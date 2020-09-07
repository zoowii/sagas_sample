package main

import (
	"log"
	"net"
	"net/http"
	"fmt"
	consulapi "github.com/hashicorp/consul/api"
)

var count int64

// consul 服务端会自己发送请求，来进行健康检查
func consulCheck(w http.ResponseWriter, r *http.Request) {

	s := "consulCheck" + fmt.Sprint(count) + "remote:" + r.RemoteAddr + " " + r.URL.String()
	fmt.Println(s)
	fmt.Fprintln(w, s)
	count++
}

func registerServer() {

	config := consulapi.DefaultConfig()
	config.Address = "localhost:8500" // TODO: load consul url from env
	client, err := consulapi.NewClient(config)
	if err != nil {
		log.Fatal("consul client error : ", err)
	}

	registration := new(consulapi.AgentServiceRegistration)
	registration.ID = "saga_server" // 服务节点的ID
	registration.Name = "SagaServer"                                     // 服务名称
	registration.Port = port                                                  // 服务端口
	registration.Tags = []string{"saga", "server"}                               // tag，可以为空
	registration.Address = "localhost"                                        // 服务 IP
	registration.Meta = map[string]string{"scheme": "grpc"}

	checkPort := 6002
	registration.Check = &consulapi.AgentServiceCheck{ // 健康检查
		HTTP:                           fmt.Sprintf("http://%s:%d%s", registration.Address, checkPort, "/check"),
		Timeout:                        "3s",
		Interval:                       "5s",  // 健康检查间隔
		DeregisterCriticalServiceAfter: "30s", //check失败后30秒删除本服务，注销时间，相当于过期时间
		// GRPC:     fmt.Sprintf("%v:%v/%v", IP, r.Port, r.Service),// grpc 支持，执行健康检查的地址，service 会传到 Health.Check 函数中
	}

	err = client.Agent().ServiceDeregister(registration.ID)
	if err != nil {
		log.Fatal("deregister server error : ", err)
	}

	err = client.Agent().ServiceRegister(registration)
	if err != nil {
		log.Fatal("register server error : ", err)
	}

	http.HandleFunc("/check", consulCheck)
	go func() {
		http.ListenAndServe(fmt.Sprintf(":%d", checkPort), nil)
	}()
}

func localIP() string {
	addrs, err := net.InterfaceAddrs()
	if err != nil {
		return ""
	}
	for _, address := range addrs {
		if ipnet, ok := address.(*net.IPNet); ok && !ipnet.IP.IsLoopback() {
			if ipnet.IP.To4() != nil {
				return ipnet.IP.String()
			}
		}
	}
	return ""
}

