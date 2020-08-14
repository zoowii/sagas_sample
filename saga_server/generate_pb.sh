#!/bin/bash
protoc  --go_out=plugins=grpc:. protos/saga.proto