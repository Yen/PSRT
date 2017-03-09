import * as proxyListener from "./listener";
import * as proxyProxy from "./proxy";
import * as net from "net";
import * as logger from "../logger";
import * as applicationResources from "../applicationResources";
import "colors";

const log = logger.createLog("Proxy Manager".yellow);

export class Manager {
	private readonly _resources: applicationResources.Resources; 

	private _proxyId = 0;

	private readonly _listeners: {
		listener: proxyListener.Listener,
		promise: Promise<void>
	}[] = [];

	public constructor(resources: applicationResources.Resources) {
		this._resources = resources;
	}

	public startListener(address: string, port: number): Promise<void> {
		// if there is already a listener on the selected port return completed
		// TODO: this might not work for everything, need to list by address also
		const found = this._listeners.find(x => x.listener.hostPort == port);
		if (found) {
			return found.promise;
		}
		log(`Starting new proxy listener -> ${address}:${port}`);

		const listener = new proxyListener.Listener(this._resources.bindAddress, port, address, port);

		listener.on("proxy", (client: net.Socket, server: net.Socket) => {
			const id = this._proxyId++;
			log(`Starting new proxy instance -> id: ${id}`)
			const proxy = new proxyProxy.Proxy(client, server, this, this._resources, logger.createLog(`Proxy #${id}`));
		});

		const promise = new Promise<void>((resolve) => {
			listener.on("listening", resolve);
		});

		listener.listen();

		this._listeners.push({
			listener: listener,
			promise: promise
		});

		return promise;
	}
}