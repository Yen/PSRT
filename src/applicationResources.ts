import * as sqlite3 from "sqlite3";
import * as logger from "./logger";
import * as fs from "fs";

export class Resources {
	private _log: logger.Callback;

	private _blockNames: Map<string, string>;
	public get blockNames() {
		return this._blockNames;
	}

	private _hostAddress: string;
	public get hostAddress() {
		return this._hostAddress;
	}

	private _bindAddress: string;
	public get bindAddress() {
		return this._bindAddress;
	}

	public constructor(log: logger.Callback = console.log) {
		this._log = logger.createLog("Resources".green, log);
	}

	public async reload() {
		await this.reloadSettings();
		await this.reloadBlockNames();
	}

	public async reloadSettings() {
		this._log("Loading settings");

		const settingsString = await new Promise<string>((resolve, reject) => {
			fs.readFile("resources/settings.json", "utf8", (err, data) => {
				if (err) {
					reject(err);
				} else {
					resolve(data);
				}
			});
		});

		const settings = JSON.parse(settingsString);

		this._hostAddress = settings.hostAddress || "127.0.0.1";
		this._bindAddress = settings.bindAddress || "127.0.0.1";
	}

	public async reloadBlockNames() {
		this._log("Loading block names");

		const db = new sqlite3.Database("resources/translations.sqlite");

		type TranslationRow = {
			original: string,
			replacement: string;
		};

		const rows = await new Promise<TranslationRow[]>((resolve, reject) => {
			db.all("SELECT original, replacement FROM block_names WHERE replacement IS NOT NULL", (err, rows) => {
				if (err) {
					reject(err);
				} else {
					resolve(rows);
				}
			});
		});

		db.close();

		this._blockNames = new Map<string, string>();
		for (const r of rows) {
			this._blockNames.set(r.original, r.replacement);
		}

		this._log(`Loaded \`${this._blockNames.size}\` block names`);
	}
}