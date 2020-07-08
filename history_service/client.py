# coding: utf8
from __future__ import print_function
import logging

import grpc

import history_pb2
import history_pb2_grpc


def run():
    # NOTE(gRPC Python Team): .close() is possible on a channel and should be
    # used in circumstances in which the with statement does not fit the needs
    # of the code.
    with grpc.insecure_channel('localhost:50051') as channel:
        stub = history_pb2_grpc.HistoryStub(channel)
        response = stub.AddOrderHistory(history_pb2.AddOrderHistoryRequest(orderId='o001', customerName='you', amount=1))
    print("history service client received: " + response.message)


if __name__ == '__main__':
    logging.basicConfig()
    run()