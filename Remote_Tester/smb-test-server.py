"""Loopback-only SMB2 guest fixture; used by native Windows and Mono checks."""
import argparse
import pathlib
from impacket.smbserver import SimpleSMBServer

parser = argparse.ArgumentParser()
parser.add_argument("root")
parser.add_argument("--port", type=int, default=24445)
args = parser.parse_args()
root = pathlib.Path(args.root).resolve()
storage = root / "storage"
storage.mkdir(parents=True, exist_ok=True)
server = SimpleSMBServer(listenAddress="127.0.0.1", listenPort=args.port)
server.addShare("EMMC Images", str(storage), readOnly="yes")
server.setSMB2Support(True)
server.setLogFile(str(root / "smb-server.log"))
print("READY", flush=True)
server.start()
