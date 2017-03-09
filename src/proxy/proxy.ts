import * as net from "net";
import * as fs from "fs";
import * as ip from "ip";
import * as proxyListener from "./listener"
import * as proxyManager from "./manager";
import * as logger from "../logger";
import * as applicationResources from "../applicationResources";
const NodeRSA = require("node-rsa");
const RC4 = require("simple-rc4");
const reverse = require("buffer-reverse");
import "colors";

export type PacketWriteCallback = (packet: Buffer) => void;

export class PacketSignature {
	public type: number;
	public subtype: number;
}

export enum ProxySource {
	Client,
	Server
}

export class Proxy {
	private readonly _resources: applicationResources.Resources;
	private readonly _manager: proxyManager.Manager;

	private readonly _decrypters: Map<ProxySource, any> = new Map();

	private readonly _client: net.Socket;
	private readonly _server: net.Socket;

	private _clientWriter: PacketWriteCallback;
	private _serverWriter: PacketWriteCallback;

	constructor(client: net.Socket, server: net.Socket, manager: proxyManager.Manager, resources: applicationResources.Resources, log: logger.Callback) {
		this._manager = manager;
		this._resources = resources;

		this._client = client;
		this._server = server;

		// default, non encrypted writers
		this._clientWriter = (packet) => this._client.write(packet);
		this._serverWriter = (packet) => this._server.write(packet);

		this._processConnection(this._client, this._server, ProxySource.Client, logger.createLog(ProxySource[ProxySource.Client].yellow, log));
		this._processConnection(this._server, this._client, ProxySource.Server, logger.createLog(ProxySource[ProxySource.Server].cyan, log));
	}

	private _processConnection(input: net.Socket, output: net.Socket, source: ProxySource, log: logger.Callback) {
		input.on("error", (err) => {
			log(`Error with socket -> ${err.message}`);
			input.end();
			output.end();
		});

		input.on("close", () => {
			input.end();
			output.end();
		});

		input.on("close", () => {
			log(`Socket closed`);
		});

		// initialize to empty buffer
		let accepted = Buffer.from([]);

		// TODO: clean up async mess
		input.on("data", (data) => {
			(async (data) => {
				const decrypter = this._decrypters.get(source);
				if (decrypter) {
					decrypter.update(data);
				}

				accepted = Buffer.concat([accepted, data]);

				// only attempt to decode if there is enough data to read packet length
				while (accepted.length >= 4) {
					const packetLength = accepted.readUInt32LE(0);

					// if there is not enough data for the whole packet go back and wait for more
					if (accepted.length < packetLength) {
						break;
					}

					const type = accepted.readUInt8(4);
					const subtype = accepted.readUInt8(5);

					const flags = accepted.readUInt16LE(6);

					log("Packet received -> "
						+ `length: ${packetLength}, `
						+ `signature: [0x${type.toString(16)}, 0x${subtype.toString(16)}], `
						+ `flags: 0x${flags.toString(16)}`);

					const packet = accepted.slice(0, packetLength);
					const body = packet.slice(8);

					accepted = accepted.slice(packetLength);

					const outputWriter = source == ProxySource.Client ? this._serverWriter : this._clientWriter;

					// temp
					if (type == 0x11 && subtype == 0x2c) {
						await this._handleBlockInfoPacket(packet, body, outputWriter);
						continue;
					} else if (type == 0x11 && subtype == 0x0b) {
						await this._handleKeyExchangePacket(packet, body, outputWriter);
						log("Encryption enabled");
						continue;
					} else if (type == 0x11 && subtype == 0x13) {
						await this._handleBlockReplyPacket(packet, body, outputWriter);
						continue;
					} else if (type == 0x11 && (subtype == 0x17 || subtype == 0x4f)) {
						await this._handleRoomInfoPacket(packet, body, outputWriter);
						continue;
					} else if (type == 0x11 && subtype == 0x21) {
						await this._handleSharedShipPacket(packet, body, outputWriter);
						continue;
					} else if (type == 0x11 && subtype == 0x01) {
						await this._handleLoginConfirmationPacket(packet, body, outputWriter, log);
						continue;
					} else if (type == 0x11 && subtype == 0x10) {
						await this._handleBlockListPacket(packet, body, outputWriter);
						continue;
					}

					outputWriter(packet);
				}
			})(data).catch((reason) => {
				log(`Error handling packet -> ${reason}`);
				input.end();
				output.end();
			});
		});
	}

	private async _handleBlockInfoPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		const blockName = body.toString("utf16le", 32, 32 + 64).split("\0", 1)[0];
		const address = ip.toString(body, 96, 4);
		const port = body.readUInt16LE(100);

		await this._manager.startListener(address, port);
		ip.toBuffer(this._resources.hostAddress).copy(body, 96);

		outputWriter(packet);
	}

	private async _handleBlockReplyPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		const address = ip.toString(body, 12, 4);
		const port = body.readUInt16LE(16);

		await this._manager.startListener(address, port);
		ip.toBuffer(this._resources.hostAddress).copy(body, 12);

		outputWriter(packet);
	}

	private async _handleRoomInfoPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		const address = ip.toString(body, 24, 4);
		const port = body.readUInt16LE(32);

		await this._manager.startListener(address, port);
		ip.toBuffer(this._resources.hostAddress).copy(body, 24);

		outputWriter(packet);
	}

	private async _handleSharedShipPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		const address = ip.toString(body, 0, 4);
		const port = body.readUInt16LE(4);

		await this._manager.startListener(address, port);
		ip.toBuffer(this._resources.hostAddress).copy(body);

		outputWriter(packet);
	}

	private async _handleLoginConfirmationPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback, log: logger.Callback) {
		const success = body.readUInt32LE(0) == 0;

		if (!success) {
			log("Login failed");
			outputWriter(packet);
			return;
		}

		const userId = body.readUInt32LE(8);
		log(`Login success -> userId: ${userId}`);

		const blockName = body.toString("utf16le", 20, 84).split("\0", 1)[0];

		const matches = blockName.match(/^B-\d\d\d:/);
		if (matches) {
			const replacementName = `${matches[0]}PSRT`;
			body.fill(0, 20, 84);
			const replacementNameBuffer = Buffer.from(replacementName, "utf16le");
			replacementNameBuffer.copy(body, 20, 0, Math.min(64, replacementNameBuffer.length));
		}

		outputWriter(packet);
	}

	private async _handleBlockListPacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		const blockCount = body.readUInt32LE(0);

		for (let i = 0; i < blockCount; i++) {
			const offset = 20 + i * 232;

			const name = body.toString("utf16le", offset, offset + 64).split("\0", 1)[0];
			const matches = name.match(/^B-\d\d\d:/);
			if (matches) {
				const title = name.substring(6, name.length);

				// translate block name here
				const replacementTitle = this._resources.blockNames.get(title);
				if (replacementTitle) {
					const replacementName = `${matches[0]}${replacementTitle}`;

					body.fill(0, offset, offset + 64);
					const replacementNameBuffer = Buffer.from(replacementName, "utf16le");
					replacementNameBuffer.copy(body, offset, 0, Math.min(64, replacementNameBuffer.length));
				}
			}
		}

		outputWriter(packet);
	}

	private async _handleKeyExchangePacket(packet: Buffer, body: Buffer, outputWriter: PacketWriteCallback) {
		// TODO: this is full of temp solutions, extract into own file
		const psrtBlob = await new Promise<Buffer>((resolve) => {
			fs.readFile("./resources/privatekey.pem", (err, data) => {
				if (err) {
					throw err;
				}
				resolve(data);
			});
		});
		const segaBlob = await new Promise<Buffer>((resolve) => {
			fs.readFile("./resources/sega-publickey.pem", (err, data) => {
				if (err) {
					throw err;
				}
				resolve(data);
			});
		});

		const psrtKey = new NodeRSA(psrtBlob, "pkcs8-private-pem", { encryptionScheme: "pkcs1" });
		const segaKey = new NodeRSA(segaBlob, "pkcs8-public-pem", { encryptionScheme: "pkcs1" });

		const encrypted = <Buffer>reverse(body.slice(0, 128));
		const decrypted = <Buffer>psrtKey.decrypt(encrypted);

		const token = decrypted.slice(0, 16);
		const rc4Key = Buffer.from(decrypted.slice(16, 32));

		const clientEncrypter = new RC4(rc4Key);
		this._clientWriter = (packet) => {
			clientEncrypter.update(packet);
			this._client.write(packet);
		}

		const serverEncrypter = new RC4(rc4Key);
		this._serverWriter = (packet) => {
			serverEncrypter.update(packet);
			this._server.write(packet);
		}

		this._decrypters.set(ProxySource.Client, new RC4(rc4Key));
		this._decrypters.set(ProxySource.Server, new RC4(rc4Key));

		const reencrypted = <Buffer>reverse(<Buffer>segaKey.encrypt(decrypted));
		reencrypted.copy(body);

		outputWriter(packet);
	}
}