#!/bin/bash
python3 -m grpc_tools.protoc -I../apis/Protos --python_out=. --grpc_python_out=. ../apis/Protos/history.proto