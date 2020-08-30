package main

import (
	pb "github.com/zoowii/saga_server/api"
	"github.com/zoowii/saga_server/app"
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
		return
	}
	log.Println(address + " net.Listing...")
	grpcServer := grpc.NewServer()

	// TODO: load config from config file or environment
	dbUrl := "root:123456@tcp(127.0.0.1)/saga_server?charset=utf8&checkConnLiveness=true&parseTime=true"

	sagaApp, err := app.NewApplicationContext(app.SetDbUrl(dbUrl))
	if err != nil {
		log.Fatalf("saga app context err: %v", err)
		return
	}
	defer sagaApp.Close()
	sagaServerService, err  := services.NewSagaServerService(sagaApp)
	if err != nil {
		log.Fatalf("saga server service err: %v", err)
		return
	}
	pb.RegisterSagaServerServer(grpcServer, sagaServerService)

	if err = grpcServer.Serve(listener); err != nil {
		log.Fatalf("grpcServer.Serve err: %v", err)
	}
}
