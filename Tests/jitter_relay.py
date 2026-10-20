"""TCP relay for ApocalypseSnow jitter tests.

Preserves byte order independently in each TCP direction. Each received chunk
is assigned an independent random delay; a dedicated writer releases chunks
at max(arrival + random_delay, previous_release) to preserve TCP ordering.
Does not drop, duplicate or modify payloads.
"""
import argparse
import asyncio
import logging
import random
import time

parser = argparse.ArgumentParser()
parser.add_argument("--listen-port", type=int, default=8081)
parser.add_argument("--target-port", type=int, default=8082)
parser.add_argument("--min-ms", type=float, default=50)
parser.add_argument("--max-ms", type=float, default=500)
parser.add_argument("--seed", type=int, default=5173)
args = parser.parse_args()
if args.min_ms < 0 or args.max_ms < args.min_ms:
    parser.error("Invalid jitter interval")

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
next_session = 0


async def direction(reader, writer, tag, seed):
    rng = random.Random(seed)
    queue = asyncio.Queue(maxsize=4096)

    async def receiver():
        try:
            while chunk := await reader.read(4096):
                now = time.monotonic()
                delay = rng.uniform(args.min_ms, args.max_ms) / 1000.0
                await queue.put((chunk, now, delay))
        finally:
            await queue.put(None)

    async def sender():
        count = 0
        last_due = 0.0
        total_bytes = 0
        try:
            while True:
                item = await queue.get()
                if item is None:
                    break
                chunk, received_at, delay = item
                last_due = max(last_due, received_at + delay)
                await asyncio.sleep(max(0, last_due - time.monotonic()))
                writer.write(chunk)
                await writer.drain()
                count += 1
                total_bytes += len(chunk)
                actual_ms = (time.monotonic() - received_at) * 1000
                logging.info(
                    "JITTER path=%s chunk=%d bytes=%d planned_ms=%.2f actual_ms=%.2f",
                    tag, count, len(chunk), delay * 1000, actual_ms
                )
        except (ConnectionError, OSError) as exc:
            logging.info("DISCONNECTED path=%s error=%s", tag, exc)
        finally:
            logging.info("SUMMARY path=%s chunks=%d bytes=%d", tag, count, total_bytes)
            writer.close()
            try:
                await writer.wait_closed()
            except (ConnectionError, OSError):
                pass

    receiver_task = asyncio.create_task(receiver())
    sender_task = asyncio.create_task(sender())
    try:
        await asyncio.gather(receiver_task, sender_task)
    finally:
        for task in (receiver_task, sender_task):
            if not task.done():
                task.cancel()


async def on_client(client_reader, client_writer):
    global next_session
    next_session += 1
    sid = next_session
    peer = client_writer.get_extra_info("peername")
    logging.info("CONNECT session=%d peer=%s", sid, peer)
    try:
        server_reader, server_writer = await asyncio.open_connection("127.0.0.1", args.target_port)
        client_writer.transport.set_write_buffer_limits(high=65536)
        server_writer.transport.set_write_buffer_limits(high=65536)
        await asyncio.gather(
            direction(client_reader, server_writer, f"{sid}:client_to_server", args.seed + sid * 19),
            direction(server_reader, client_writer, f"{sid}:server_to_client", args.seed + sid * 19 + 1),
        )
    except Exception:
        logging.exception("SESSION_ERROR session=%d", sid)
    finally:
        client_writer.close()
        try:
            await client_writer.wait_closed()
        except (ConnectionError, OSError):
            pass
        logging.info("CLOSE session=%d", sid)


async def main():
    srv = await asyncio.start_server(on_client, "127.0.0.1", args.listen_port)
    logging.info("READY 127.0.0.1:%d -> 127.0.0.1:%d random_delay=[%.0f,%.0f]ms seed=%d",
                 args.listen_port, args.target_port, args.min_ms, args.max_ms, args.seed)
    async with srv:
        await srv.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
