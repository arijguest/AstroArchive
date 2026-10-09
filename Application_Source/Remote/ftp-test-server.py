"""Read-only loopback FTP fixture; no telescope or user directories are touched."""
import pathlib
import sys
from pyftpdlib.authorizers import DummyAuthorizer
from pyftpdlib.handlers import FTPHandler
from pyftpdlib.servers import FTPServer

root, port, ready = sys.argv[1:]
pathlib.Path(root).mkdir(parents=True, exist_ok=True)
authorizer = DummyAuthorizer()
authorizer.add_anonymous(root, perm="elr")
FTPHandler.authorizer = authorizer
server = FTPServer(("127.0.0.1", int(port)), FTPHandler)
pathlib.Path(ready).write_text("ready")
server.serve_forever(timeout=0.1)
