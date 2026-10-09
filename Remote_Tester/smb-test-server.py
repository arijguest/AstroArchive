"""Loopback-only SMB2 guest fixture; used by native Windows and Mono checks."""
import argparse
import os
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
# Impacket 0.13.1's read-only CREATE branch drops O_BINARY on Windows.
# Correct only the fixture's opened file handles; the client remains unchanged.
if os.name == "nt":
    import msvcrt
    from impacket import smb3structs

    backend = server.getServer()

    def binary_create(connection_id, smb_server, packet):
        response = original_create(connection_id, smb_server, packet)
        for opened in smb_server.getConnectionData(connection_id)["OpenedFiles"].values():
            handle = opened.get("FileHandle")
            if isinstance(handle, int) and handle >= 0:
                msvcrt.setmode(handle, os.O_BINARY)
        return response

    original_create = backend.hookSmb2Command(smb3structs.SMB2_CREATE, binary_create)
print("READY", flush=True)
server.start()
