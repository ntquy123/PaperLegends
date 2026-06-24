import cluster, { Worker } from 'cluster';
import os from 'os';
import net from 'net';

const resolveWorkerCount = (): number => {
  const cpuCount = Math.max(1, os.cpus().length);
  const requested = process.env.WEB_CONCURRENCY ?? process.env.CLUSTER_WORKERS;

  if (!requested) {
    return 1;
  }

  if (requested.toLowerCase() === 'auto') {
    return cpuCount;
  }

  const count = Number(requested);
  if (!Number.isFinite(count) || count < 1) {
    return 1;
  }

  return Math.min(Math.floor(count), cpuCount);
};

const getWorkerIndex = (ip: string, length: number): number => {
  let hash = 0;
  for (let i = 0; i < ip.length; i++) {
    const char = ip.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash |= 0;
  }
  return Math.abs(hash) % length;
};

export const initCluster = (apiPort: number) => {
  const numWorkers = resolveWorkerCount();
  const workers: Worker[] = [];

  for (let i = 0; i < numWorkers; i++) {
    workers[i] = cluster.fork();
  }

  cluster.on('exit', (worker) => {
    console.log(`Worker ${worker.process.pid} exited. Spawning a new process.`);
    const index = workers.indexOf(worker);
    workers[index] = cluster.fork();
  });

  const server = net.createServer({ pauseOnConnect: true }, (socket) => {
    const ip = socket.remoteAddress || '';
    const worker = workers[getWorkerIndex(ip, workers.length)];
    worker.send('sticky-session:connection', socket);
  });

  server.listen(apiPort, () => {
    console.log(`Master listening on API port ${apiPort} with ${numWorkers} worker(s)`);
  });
};

export default initCluster;
