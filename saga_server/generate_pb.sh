#!/bin/bash
protoc  --go_out=plugins=grpc:./api protos/saga.proto