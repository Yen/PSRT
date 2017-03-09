export type Callback = (message: any) => void;

export function createLog(prefix: string, output: Callback = console.log): Callback {
	return (message: any) => {
		output(`[${prefix}] ${message}`);
	};
}