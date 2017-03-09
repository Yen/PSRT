import * as events from "events";
import * as net from "net";

export class Listener extends events.EventEmitter {
	private readonly _server: net.Server;

	public readonly localAddress: string;
	public readonly localPort: number;

	public readonly hostAddress: string;
	public readonly hostPort: number;

	public constructor(localAddress: string, localPort: number, hostAddress: string, hostPort: number) {
		super();

		this.localAddress = localAddress;
		this.localPort = localPort;
		this.hostAddress = hostAddress;
		this.hostPort = hostPort;

		this._server = net.createServer();
		this._server.on("listening", () => this.emit("listening"));
		this._server.on("connection", (client) => {
			const server = net.createConnection(this.hostPort, this.hostAddress);

			client.setNoDelay();
			server.setNoDelay();

			this.emit("proxy", client, server);
		});
	}

	public listen() {
		return new Promise((resolve) => {
			this._server.listen(this.localPort, this.localAddress, resolve);
		});
	}
}