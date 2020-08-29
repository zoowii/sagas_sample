CREATE TABLE `global_tx` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `xid` varchar(50) NOT NULL,
  `state` int(11) NOT NULL,
  `version` int(11) NOT NULL,
  `creator_group` varchar(100) DEFAULT NULL,
  `creator_service` varchar(100) DEFAULT NULL,
  `creator_instance_id` varchar(100) DEFAULT NULL,
  `expire_seconds` int(11) NOT NULL,
  `extra` text,
  PRIMARY KEY (`id`),
  UNIQUE KEY `global_tx_index_xid` (`xid`),
  KEY `global_tx_index_creator_group_creator_service` (`creator_group`,`creator_service`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;



CREATE TABLE `branch_tx` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `branch_tx_id` varchar(50) NOT NULL,
  `xid` varchar(50) NOT NULL,
  `state` int(11) NOT NULL,
  `version` int(11) NOT NULL,
  `compensation_fail_times` int(11) NOT NULL,
  `node_group` varchar(100) DEFAULT NULL,
  `node_service` varchar(100) DEFAULT NULL,
  `node_instance_id` varchar(100) DEFAULT NULL,
  `branch_service_key` varchar(255) DEFAULT NULL,
  `branch_compensation_service_key` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `branch_tx_idx_branch_tx_id` (`branch_tx_id`) /*!80000 INVISIBLE */,
  KEY `branch_tx_idx_xid` (`xid`) /*!80000 INVISIBLE */,
  KEY `branch_tx_node_group_node_service` (`node_group`,`node_service`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `tx_log` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `xid` varchar(50) NOT NULL,
  `branch_tx_id` varchar(50) DEFAULT NULL,
  `operator_group` varchar(100) DEFAULT NULL,
  `operator_service` varchar(100) DEFAULT NULL,
  `operator_instance_id` varchar(100) DEFAULT NULL,
  `log_type` varchar(45) DEFAULT NULL,
  `log_params` text,
  PRIMARY KEY (`id`),
  KEY `tx_log_idx_xid_branch_x_id` (`xid`,`branch_tx_id`),
  KEY `tx_log_idx_branch_tx_id` (`branch_tx_id`) /*!80000 INVISIBLE */
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

