package main

import (
	pb "github.com/zoowii/saga_server/api"
	services "github.com/zoowii/saga_server/services"
	grpc "google.golang.org/grpc"
	"log"
	"net"
	_ "net/http/pprof"
)

const (
	address = ":9009"
	network = "tcp"
)

func main() {
	listener, err := net.Listen(network, address)
	if err != nil {
		log.Fatalf("net.Listen err: %v", err)
	}
	log.Println(address + " net.Listing...")
	grpcServer := grpc.NewServer()
	pb.RegisterSagaServerServer(grpcServer, &services.SagaServerService{})

	if err = grpcServer.Serve(listener); err != nil {
		log.Fatalf("grpcServer.Serve err: %v", err)
	}
}
