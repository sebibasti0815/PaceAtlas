import { ensurePaceAtlas } from "./launch";
import net from "node:net";

const PIPE = "\\\\.\\pipe\\PaceAtlas.Api.v1";
let nextId = 1;
async function callOnce<T>(method: string, params: object): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const socket = net.createConnection(PIPE);
    const timer = setTimeout(() => socket.destroy(new Error("PaceAtlas antwortet nicht (Timeout).")), 5000);
    let buffer = Buffer.alloc(0);
    let finished = false;
    const fail = (error: Error) => { if (!finished) { finished = true; clearTimeout(timer); reject(error); } };
    socket.on("error", fail);
    socket.on("close", () => { if (!finished) fail(new Error("PaceAtlas ist nicht gestartet oder die Verbindung wurde beendet.")); });
    socket.on("connect", () => {
      const body = Buffer.from(JSON.stringify({ jsonrpc: "2.0", id: nextId++, method, params }), "utf8");
      const prefix = Buffer.alloc(4); prefix.writeInt32LE(body.length, 0);
      socket.write(Buffer.concat([prefix, body]));
    });
    socket.on("data", (part) => {
      buffer = Buffer.concat([buffer, part]);
      if (buffer.length < 4) return;
      const length = buffer.readInt32LE(0);
      if (length < 1 || length > 1048576) { socket.destroy(new Error("Ungültige Antwortlänge.")); return; }
      if (buffer.length < length + 4) return;
      try {
        const reply = JSON.parse(buffer.subarray(4, 4 + length).toString("utf8"));
        finished = true; clearTimeout(timer); socket.end();
        if (reply.error) reject(new Error(reply.error.message)); else resolve(reply.result as T);
      } catch (error) { fail(error as Error); socket.destroy(); }
    });
  });
}

// ENOENT may occur briefly while the WinUI app creates its first listener.
// Retry connection failures only; never replay an operation after it may have reached the server.
export async function call<T>(method: string, params: object = {}): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try { return await callOnce<T>(method, params); }
    catch (error) {
      const message = String(error);
      if (attempt >= 3 || !/ENOENT|ECONNREFUSED|EPIPE/.test(message)) throw error;
      if (attempt === 0 && /ENOENT|ECONNREFUSED/.test(message)) await ensurePaceAtlas();
      await new Promise(resolve => setTimeout(resolve, 150 * (attempt + 1)));
    }
  }
}
