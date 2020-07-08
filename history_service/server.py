# coding: utf8
import history_pb2_grpc
import history_pb2
import grpc
import logging
from concurrent import futures
import consul
import threading
import time


class HistoryService(history_pb2_grpc.HistoryServicer):
    def AddOrderHistory(self, request, context):
        print(f'AddOrderHistory')
        return history_pb2.AddOrderHistoryReply(success=True, message='done')

    def CancelOrderHistory(self, request, context):
        print(f'CancelOrderHistory')
        return history_pb2.CancelOrderHistoryReply(success=True, message='done')


class ConsulTtlThread(threading.Thread):
    def __init__(self, c, ttl_check_id, interval):
        threading.Thread.__init__(self)
        self.c = c
        self.ttl_check_id = ttl_check_id
        self.interval = interval if interval >= 1 else 1
        self.running = True
        print('ttl_check_id', self.ttl_check_id)

    def run(self):
        while self.running:
            # print('sending ttl')
            res = self.c.agent.check.ttl_pass(self.ttl_check_id)
            # print('ttl res', res)
            time.sleep(self.interval)


def get_ttl_check_id(checks, service_id):
    for check_id in checks.keys():
        value = checks[check_id]
        if value['ServiceID'] == service_id and value['Type'] == 'ttl':
            return check_id
    return None


def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    history_pb2_grpc.add_HistoryServicer_to_server(HistoryService(), server)
    host = '0.0.0.0'
    service_address = '127.0.0.1'
    port = 50051
    ttl = 10
    server.add_insecure_port(f'{host}:{port}')
    print(f'server listening on {host}:{port}')

    c = consul.Consul(host='127.0.0.1', port=8500, scheme='http')

    service_name = 'history.service'
    service_id = 'history.service-001'
    c.agent.service.deregister(service_id=service_id)
    c.agent.service.register(service_name, service_id=service_id,
                             address=service_address, port=port,
                             tags=['saga', 'api', 'example', 'python'],
                             check=consul.Check.ttl('10s'))
    checks = c.agent.checks()
    ttl_check_id = get_ttl_check_id(checks, service_id)

    t = ConsulTtlThread(c, ttl_check_id, ttl / 2)
    t.start()
    try:
        server.start()
        server.wait_for_termination()
    finally:
        t.running = False
        c.agent.service.deregister(service_id=service_id)


if __name__ == '__main__':
    logging.basicConfig()
    serve()
