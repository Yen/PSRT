import * as net from "net";
import * as proxyManager from "./proxy/manager";
import * as applicationResources from "./applicationResources";
import * as logger from "./logger";

console.log(`PSRT ${process.env.npm_package_version}`);

(async () => {
	const resources = new applicationResources.Resources();
	await resources.reload();

	const manager = new proxyManager.Manager(resources);

	// ship2
	await manager.startListener("210.189.208.16", 12200);
})();