-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Generation Time: Oct 29, 2026 at 05:54 AM
-- Server version: 8.0.46
-- PHP Version: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `inventory_db`
--

-- --------------------------------------------------------

--
-- Table structure for table `tbl_account`
--

CREATE TABLE `tbl_account` (
  `acc_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `acc_name` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `user_name` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `pass_word` varchar(255) COLLATE utf8mb4_general_ci NOT NULL,
  `gender` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `acc_type` enum('Admin','Cashier','Inventory Staff','Manager') COLLATE utf8mb4_general_ci NOT NULL,
  `phone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `email` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `user_status` varchar(10) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'Active',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_account`
--

INSERT INTO `tbl_account` (`acc_id`, `acc_name`, `user_name`, `pass_word`, `gender`, `acc_type`, `phone`, `email`, `user_status`, `created_at`) VALUES
('303303', 'Irisa Abenes', 'irish', '100000.H1LodDznx3y/XTk+PRVt1A==.JCI4SpZwbIwBFVJt/6CMMBwZGSL99dsqR4HzUmrMI3A=', 'Male', 'Admin', '0923225261', 'asdjaj@gmail.com', 'Active', '2026-09-28 10:20:29'),
('ACC-001', 'Ashreiel Azucena', 'wailhi', '100000.gtF+APOSZUd8WUI53d/oVg==.qeiv1D28GhraEmGgttNoBL1iAn3jZt6RZ9988WjvAew=', 'Male', 'Admin', '09163983310', 'aashreiel@gmail.com', 'Active', '2026-09-15 12:34:49'),
('ACC-002', 'Marlon', 'akoni', '100000.TcCL7jiPcpNZPJDhE6f/mA==.xKW1XmZy8wgKp82yleK9mAds6xmXnXvRkyJCa8JBV4o=', 'Male', 'Manager', '0123456789', 'ash@gmail.com', 'Active', '2026-09-15 14:33:13'),
('ACC-003', 'Ashley Azucena', 'maoni', '100000.shjyn8t2PpRNmrGktPReng==.pSQTm9r3CVpc8wab3cnHxzU6eC7/AMebiwm6m5WZgmc=', 'Female', 'Cashier', '09163983310', 'ashreiel@gmail.com', 'Active', '2026-09-16 12:16:52'),
('ACC-004', 'junsin cabardo', 'maisha', '100000.zhjRCMr4oH2IBPXoWV105w==.THuq9GQyV5C+B7FbEIsdRJTLggdr0VVPiJQaaKPGg9k=', 'Male', 'Cashier', '09463168397', 'abenes123@gmail.com', 'Active', '2026-10-28 15:38:41'),
('ACC-005', 'carlito say', 'irisha', '100000.JbuHDTHQb6IcCSYYmwIcWg==.P4RrC4qjnZsgAcB9/QirAdvxok9qV7zVy0kCr7jjOy4=', 'Male', 'Cashier', '09464502356', 'irishabenes@gmail.com', 'Active', '2026-10-28 15:39:49');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_category`
--

CREATE TABLE `tbl_category` (
  `category_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `category_name` varchar(100) COLLATE utf8mb4_general_ci NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_category`
--

INSERT INTO `tbl_category` (`category_id`, `category_name`) VALUES
('CAT-0011', 'Baby Products'),
('CAT-0001', 'Beverages'),
('CAT-0005', 'Candies & Chocolates'),
('CAT-0004', 'Canned Goods'),
('CAT-0008', 'Cooking Essentials'),
('CAT-0006', 'Dairy & Chilled'),
('CAT-0007', 'Frozen Foods'),
('CAT-0010', 'Household'),
('CAT-0003', 'Instant Food'),
('CAT-0009', 'Personal Care'),
('CAT-0012', 'Pet Supplies'),
('CAT-0013', 'School & Office'),
('CAT-0002', 'Snacks');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_product`
--

CREATE TABLE `tbl_product` (
  `product_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `product_name` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `barcode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `category_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `supplier_id` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `cost_price` decimal(12,2) NOT NULL DEFAULT '0.00',
  `selling_price` decimal(12,2) NOT NULL DEFAULT '0.00',
  `product_status` varchar(10) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'Active',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_product`
--

INSERT INTO `tbl_product` (`product_id`, `product_name`, `barcode`, `category_id`, `supplier_id`, `cost_price`, `selling_price`, `product_status`, `created_at`) VALUES
('PRD-0001', 'Super crunch Special Chip-Charon', '4800365701088', 'CAT-0002', NULL, 25.00, 30.00, 'Inactive', '2026-09-15 12:41:31'),
('PRD-0002', 'Nature Spring (500ml)', '4800049720114', 'CAT-0001', NULL, 8.00, 10.00, 'Active', '2026-09-15 14:31:21'),
('PRD-0003', 'Joy Anti-Bact', '4902430468763', 'CAT-0010', NULL, 250.00, 300.00, 'Active', '2026-09-15 18:34:19'),
('PRD-0004', 'Bear Brand', NULL, 'CAT-0006', NULL, 125.00, 130.00, 'Active', '2026-10-03 15:06:59'),
('PRD-0005', 'Red Horse', NULL, 'CAT-0001', NULL, 110.00, 135.00, 'Active', '2026-10-07 16:48:18'),
('PRD-0006', 'Piatos', NULL, 'CAT-0002', NULL, 23.00, 25.00, 'Active', '2026-10-12 15:58:25'),
('PRD-0007', 'Mountain Dew (290ml)', '4803925130328', 'CAT-0001', NULL, 20.00, 25.00, 'Active', '2026-10-22 12:15:13'),
('PRD-0008', 'Nature Spring(350ml)', '4800049720107', 'CAT-0001', NULL, 12.00, 13.00, 'Active', '2026-10-22 13:49:34'),
('PRD-0009', 'Pilot Pen', '4902505088179', 'CAT-0013', NULL, 18.75, 22.00, 'Active', '2026-10-26 14:54:08'),
('PRD-0010', 'Artline ink refil(black)', '4974052811425', 'CAT-0013', NULL, 25.00, 30.00, 'Active', '2026-10-29 09:47:02');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_sale`
--

CREATE TABLE `tbl_sale` (
  `sale_id` varchar(20) COLLATE utf8mb4_general_ci NOT NULL,
  `cashier_id` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `discount_amount` decimal(12,2) NOT NULL DEFAULT '0.00',
  `discount_type` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `discount_approved_by` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `tax_amount` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_amount` decimal(12,2) NOT NULL DEFAULT '0.00',
  `amount_paid` decimal(12,2) NOT NULL DEFAULT '0.00',
  `change_due` decimal(12,2) NOT NULL DEFAULT '0.00',
  `payment_method` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'Cash',
  `sale_status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'Completed',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_sale`
--

INSERT INTO `tbl_sale` (`sale_id`, `cashier_id`, `subtotal`, `discount_amount`, `discount_type`, `discount_approved_by`, `tax_amount`, `total_amount`, `amount_paid`, `change_due`, `payment_method`, `sale_status`, `created_at`) VALUES
('SALE-000001', 'ACC-001', 90.00, 0.00, NULL, NULL, 0.00, 90.00, 1000.00, 910.00, 'Cash', 'Completed', '2026-09-15 12:43:21'),
('SALE-000002', 'ACC-001', 30.00, 0.00, NULL, NULL, 0.00, 30.00, 30.00, 0.00, 'Cash', 'Completed', '2026-09-15 12:50:29'),
('SALE-000003', 'ACC-001', 100.00, 20.00, 'Senior/PWD', 'ACC-002', 0.00, 80.00, 100.00, 20.00, 'Cash', 'Completed', '2026-09-15 14:39:13'),
('SALE-000004', 'ACC-001', 600.00, 0.00, NULL, NULL, 0.00, 600.00, 1000.00, 400.00, 'Cash', 'Completed', '2026-09-15 18:35:16'),
('SALE-000005', '303303', 320.00, 20.00, 'Other', '303303', 0.00, 300.00, 1000.00, 700.00, 'Cash', 'Completed', '2026-10-03 15:35:03'),
('SALE-000006', '303303', 110.00, 20.00, 'Other', '303303', 0.00, 90.00, 1000.00, 910.00, 'Cash', 'Completed', '2026-10-03 15:54:47'),
('SALE-000007', '303303', 300.00, 20.00, 'Manager Discount', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-07 17:05:32'),
('SALE-000008', '303303', 300.00, 20.00, 'Other', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-12 15:40:02'),
('SALE-000009', '303303', 130.00, 20.00, 'Other', '303303', 0.00, 110.00, 1000.00, 890.00, 'Cash', 'Completed', '2026-10-12 15:56:57'),
('SALE-000010', '303303', 300.00, 20.00, 'Other', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-14 15:13:20'),
('SALE-000011', '303303', 300.00, 20.00, 'Other', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-14 15:33:37'),
('SALE-000012', '303303', 325.00, 0.00, NULL, NULL, 0.00, 325.00, 350.00, 25.00, 'Cash', 'Completed', '2026-10-14 15:48:04'),
('SALE-000013', '303303', 165.00, 0.00, NULL, NULL, 0.00, 165.00, 170.00, 5.00, 'Cash', 'Completed', '2026-10-14 15:52:51'),
('SALE-000014', '303303', 440.00, 0.00, NULL, NULL, 0.00, 440.00, 450.00, 10.00, 'Cash', 'Completed', '2026-10-14 15:55:42'),
('SALE-000015', '303303', 85.00, 0.00, NULL, NULL, 0.00, 85.00, 100.00, 15.00, 'Cash', 'Completed', '2026-10-14 15:57:29'),
('SALE-000016', '303303', 10.00, 0.00, NULL, NULL, 0.00, 10.00, 1000.00, 990.00, 'Cash', 'Completed', '2026-10-14 15:59:26'),
('SALE-000017', '303303', 435.00, 0.00, NULL, NULL, 0.00, 435.00, 500.00, 65.00, 'Cash', 'Completed', '2026-10-14 16:19:38'),
('SALE-000018', '303303', 300.00, 20.00, 'Other', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-14 16:30:07'),
('SALE-000019', '303303', 50.00, 0.00, NULL, NULL, 0.00, 50.00, 60.00, 10.00, 'Cash', 'Completed', '2026-10-22 12:15:47'),
('SALE-000020', '303303', 250.00, 0.00, NULL, NULL, 0.00, 250.00, 300.00, 50.00, 'Cash', 'Completed', '2026-10-22 13:51:32'),
('SALE-000021', '303303', 90.00, 0.00, NULL, NULL, 0.00, 90.00, 100.00, 10.00, 'Cash', 'Completed', '2026-10-22 14:04:29'),
('SALE-000022', '303303', 70.00, 0.00, NULL, NULL, 0.00, 70.00, 100.00, 30.00, 'Cash', 'Completed', '2026-10-22 14:07:35'),
('SALE-000023', '303303', 635.00, 0.00, NULL, NULL, 0.00, 635.00, 1000.00, 365.00, 'Cash', 'Completed', '2026-10-22 14:16:43'),
('SALE-000024', '303303', 810.00, 0.00, NULL, NULL, 0.00, 810.00, 820.00, 10.00, 'Cash', 'Completed', '2026-10-22 14:25:21'),
('SALE-000025', '303303', 860.00, 0.00, NULL, NULL, 0.00, 860.00, 900.00, 40.00, 'Cash', 'Completed', '2026-10-22 14:28:36'),
('SALE-000026', '303303', 950.00, 0.00, NULL, NULL, 0.00, 950.00, 1000.00, 50.00, 'Cash', 'Completed', '2026-10-22 14:34:15'),
('SALE-000027', '303303', 650.00, 0.00, NULL, NULL, 0.00, 650.00, 700.00, 50.00, 'Cash', 'Completed', '2026-10-22 14:40:41'),
('SALE-000028', '303303', 300.00, 20.00, 'Other', '303303', 0.00, 280.00, 1000.00, 720.00, 'Cash', 'Completed', '2026-10-24 12:56:20'),
('SALE-000029', '303303', 135.00, 0.20, 'Other', '303303', 0.00, 134.80, 1000.00, 865.20, 'Cash', 'Completed', '2026-10-24 12:58:10'),
('SALE-000030', '303303', 300.00, 0.20, 'Senior/PWD', '303303', 0.00, 299.80, 1000.00, 700.20, 'Cash', 'Completed', '2026-10-26 11:54:12'),
('SALE-000031', 'ACC-001', 215.00, 0.00, NULL, NULL, 0.00, 215.00, 350.00, 135.00, 'Cash', 'Completed', '2026-10-29 09:51:43'),
('SALE-000032', 'ACC-001', 120.00, 0.00, NULL, NULL, 0.00, 120.00, 150.00, 30.00, 'Cash', 'Completed', '2026-10-29 09:55:31');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_sale_item`
--

CREATE TABLE `tbl_sale_item` (
  `sale_item_id` bigint UNSIGNED NOT NULL,
  `sale_id` varchar(20) COLLATE utf8mb4_general_ci NOT NULL,
  `product_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `quantity` int NOT NULL,
  `unit_price` decimal(12,2) NOT NULL,
  `line_discount` decimal(12,2) NOT NULL DEFAULT '0.00',
  `line_total` decimal(12,2) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_sale_item`
--

INSERT INTO `tbl_sale_item` (`sale_item_id`, `sale_id`, `product_id`, `quantity`, `unit_price`, `line_discount`, `line_total`) VALUES
(1, 'SALE-000001', 'PRD-0001', 3, 30.00, 0.00, 90.00),
(2, 'SALE-000002', 'PRD-0001', 1, 30.00, 0.00, 30.00),
(3, 'SALE-000003', 'PRD-0002', 10, 10.00, 0.00, 100.00),
(4, 'SALE-000004', 'PRD-0003', 2, 300.00, 0.00, 600.00),
(5, 'SALE-000005', 'PRD-0002', 2, 10.00, 0.00, 20.00),
(6, 'SALE-000005', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(7, 'SALE-000006', 'PRD-0004', 1, 130.00, 20.00, 110.00),
(8, 'SALE-000007', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(9, 'SALE-000008', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(10, 'SALE-000009', 'PRD-0004', 1, 130.00, 0.00, 130.00),
(11, 'SALE-000010', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(12, 'SALE-000011', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(13, 'SALE-000012', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(14, 'SALE-000012', 'PRD-0006', 1, 25.00, 0.00, 25.00),
(15, 'SALE-000013', 'PRD-0002', 1, 10.00, 0.00, 10.00),
(16, 'SALE-000013', 'PRD-0006', 1, 25.00, 0.00, 25.00),
(17, 'SALE-000013', 'PRD-0004', 1, 130.00, 0.00, 130.00),
(18, 'SALE-000014', 'PRD-0002', 1, 10.00, 0.00, 10.00),
(19, 'SALE-000014', 'PRD-0004', 1, 130.00, 0.00, 130.00),
(20, 'SALE-000014', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(21, 'SALE-000015', 'PRD-0002', 1, 10.00, 0.00, 10.00),
(22, 'SALE-000015', 'PRD-0006', 3, 25.00, 0.00, 75.00),
(23, 'SALE-000016', 'PRD-0002', 1, 10.00, 0.00, 10.00),
(24, 'SALE-000017', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(25, 'SALE-000017', 'PRD-0005', 1, 135.00, 0.00, 135.00),
(26, 'SALE-000018', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(27, 'SALE-000019', 'PRD-0007', 2, 25.00, 0.00, 50.00),
(28, 'SALE-000020', 'PRD-0008', 25, 10.00, 0.00, 250.00),
(29, 'SALE-000021', 'PRD-0008', 9, 10.00, 0.00, 90.00),
(30, 'SALE-000022', 'PRD-0008', 7, 10.00, 0.00, 70.00),
(31, 'SALE-000023', 'PRD-0004', 2, 130.00, 0.00, 260.00),
(32, 'SALE-000023', 'PRD-0007', 2, 25.00, 0.00, 50.00),
(33, 'SALE-000023', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(34, 'SALE-000023', 'PRD-0006', 1, 25.00, 0.00, 25.00),
(35, 'SALE-000024', 'PRD-0008', 1, 10.00, 0.00, 10.00),
(36, 'SALE-000024', 'PRD-0007', 2, 25.00, 0.00, 50.00),
(37, 'SALE-000024', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(38, 'SALE-000024', 'PRD-0004', 1, 130.00, 0.00, 130.00),
(39, 'SALE-000024', 'PRD-0005', 2, 135.00, 0.00, 270.00),
(40, 'SALE-000024', 'PRD-0006', 2, 25.00, 0.00, 50.00),
(41, 'SALE-000025', 'PRD-0007', 2, 25.00, 0.00, 50.00),
(42, 'SALE-000025', 'PRD-0003', 2, 300.00, 0.00, 600.00),
(43, 'SALE-000025', 'PRD-0008', 5, 10.00, 0.00, 50.00),
(44, 'SALE-000025', 'PRD-0006', 1, 25.00, 0.00, 25.00),
(45, 'SALE-000025', 'PRD-0005', 1, 135.00, 0.00, 135.00),
(46, 'SALE-000026', 'PRD-0008', 3, 10.00, 0.00, 30.00),
(47, 'SALE-000026', 'PRD-0005', 2, 135.00, 0.00, 270.00),
(48, 'SALE-000026', 'PRD-0006', 2, 25.00, 0.00, 50.00),
(49, 'SALE-000026', 'PRD-0003', 2, 300.00, 0.00, 600.00),
(50, 'SALE-000027', 'PRD-0003', 2, 300.00, 0.00, 600.00),
(51, 'SALE-000027', 'PRD-0007', 2, 25.00, 0.00, 50.00),
(52, 'SALE-000028', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(53, 'SALE-000029', 'PRD-0005', 1, 135.00, 0.00, 135.00),
(54, 'SALE-000030', 'PRD-0003', 1, 300.00, 0.00, 300.00),
(55, 'SALE-000031', 'PRD-0010', 5, 30.00, 0.00, 150.00),
(56, 'SALE-000031', 'PRD-0008', 5, 13.00, 0.00, 65.00),
(57, 'SALE-000032', 'PRD-0010', 4, 30.00, 0.00, 120.00);

-- --------------------------------------------------------

--
-- Table structure for table `tbl_stock`
--

CREATE TABLE `tbl_stock` (
  `stock_id` int UNSIGNED NOT NULL,
  `product_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `quantity` int NOT NULL DEFAULT '0',
  `low_stock_threshold` int NOT NULL DEFAULT '10',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_stock`
--

INSERT INTO `tbl_stock` (`stock_id`, `product_id`, `quantity`, `low_stock_threshold`, `updated_at`) VALUES
(1, 'PRD-0002', 0, 10, '2026-10-14 15:59:26'),
(2, 'PRD-0001', 1, 10, '2026-09-21 15:35:56'),
(3, 'PRD-0003', 251, 10, '2026-10-29 09:47:48'),
(5, 'PRD-0004', 23, 10, '2026-10-28 16:31:38'),
(10, 'PRD-0005', 1, 10, '2026-10-28 16:43:33'),
(15, 'PRD-0006', 1, 10, '2026-10-22 14:34:15'),
(33, 'PRD-0007', 1, 10, '2026-10-28 16:39:54'),
(36, 'PRD-0008', 0, 12, '2026-10-29 09:51:43'),
(67, 'PRD-0009', 30, 10, '2026-10-26 14:54:08'),
(84, 'PRD-0010', 241, 10, '2026-10-29 09:55:31');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_stock_log`
--

CREATE TABLE `tbl_stock_log` (
  `log_id` bigint UNSIGNED NOT NULL,
  `product_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `movement_type` enum('IN','OUT') COLLATE utf8mb4_general_ci NOT NULL,
  `reason` varchar(30) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'PURCHASE, SALE, RETURN, DAMAGED, ADJUSTMENT, EXPIRED',
  `quantity` int NOT NULL,
  `batch_no` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `expiry_date` date DEFAULT NULL,
  `unit_cost` decimal(12,2) DEFAULT NULL,
  `supplier_id` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `reference_id` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'e.g. sale_id or PO number',
  `performed_by` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `remarks` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

--
-- Dumping data for table `tbl_stock_log`
--

INSERT INTO `tbl_stock_log` (`log_id`, `product_id`, `movement_type`, `reason`, `quantity`, `batch_no`, `expiry_date`, `unit_cost`, `supplier_id`, `reference_id`, `performed_by`, `remarks`, `created_at`) VALUES
(1, 'PRD-0001', 'IN', 'PURCHASE', 4, 'BATCH-20260915124131', '2026-09-23', 25.00, NULL, NULL, 'ACC-001', 'Initial stock on product creation', '2026-09-15 12:41:31'),
(2, 'PRD-0001', 'IN', 'PURCHASE', 1, 'BATCH-20260915125110', '2027-09-15', 25.00, NULL, NULL, 'ACC-001', 'Restock via Manage Product', '2026-09-15 12:51:10'),
(3, 'PRD-0002', 'IN', 'PURCHASE', 20, 'BATCH-20260915143121', '2027-09-15', 8.00, NULL, NULL, 'ACC-001', 'Initial stock on product creation', '2026-09-15 14:31:21'),
(4, 'PRD-0003', 'IN', 'PURCHASE', 25, 'BATCH-20260915183419', '2027-09-15', 250.00, NULL, NULL, 'ACC-001', 'Initial stock on product creation', '2026-09-15 18:34:19'),
(8, 'PRD-0001', 'OUT', 'SALE', 3, NULL, NULL, NULL, NULL, 'SALE-000001', 'ACC-001', 'POS sale', '2026-09-15 12:43:21'),
(9, 'PRD-0001', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000002', 'ACC-001', 'POS sale', '2026-09-15 12:50:29'),
(10, 'PRD-0002', 'OUT', 'SALE', 10, NULL, NULL, NULL, NULL, 'SALE-000003', 'ACC-001', 'POS sale', '2026-09-15 14:39:13'),
(11, 'PRD-0003', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000004', 'ACC-001', 'POS sale', '2026-09-15 18:35:16'),
(12, 'PRD-0002', 'OUT', 'DAMAGED', 4, NULL, NULL, NULL, NULL, NULL, 'ACC-001', 'broken cap', '2026-09-17 17:50:07'),
(15, 'PRD-0003', 'OUT', 'DAMAGED', 3, NULL, NULL, NULL, NULL, NULL, 'ACC-001', NULL, '2026-09-21 15:57:48'),
(16, 'PRD-0004', 'IN', 'PURCHASE', 7, 'BATCH-20261003150648', '2027-10-03', 125.00, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-03 15:06:59'),
(17, 'PRD-0002', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000005', '303303', 'POS sale', '2026-10-03 15:35:03'),
(18, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000005', '303303', 'POS sale', '2026-10-03 15:35:03'),
(19, 'PRD-0004', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000006', '303303', 'POS sale', '2026-10-03 15:54:47'),
(20, 'PRD-0005', 'IN', 'PURCHASE', 14, 'BATCH-20261007164805', '2027-10-07', 110.00, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-07 16:48:18'),
(21, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000007', '303303', 'POS sale', '2026-10-07 17:05:32'),
(22, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000008', '303303', 'POS sale', '2026-10-12 15:40:02'),
(23, 'PRD-0004', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000009', '303303', 'POS sale', '2026-10-12 15:56:57'),
(24, 'PRD-0006', 'IN', 'PURCHASE', 12, 'BATCH-20261012155821', '2027-10-12', 23.00, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-12 15:58:25'),
(25, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000010', '303303', 'POS sale', '2026-10-14 15:13:20'),
(26, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000011', '303303', 'POS sale', '2026-10-14 15:33:37'),
(27, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000012', '303303', 'POS sale', '2026-10-14 15:48:04'),
(28, 'PRD-0006', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000012', '303303', 'POS sale', '2026-10-14 15:48:04'),
(29, 'PRD-0002', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000013', '303303', 'POS sale', '2026-10-14 15:52:51'),
(30, 'PRD-0006', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000013', '303303', 'POS sale', '2026-10-14 15:52:51'),
(31, 'PRD-0004', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000013', '303303', 'POS sale', '2026-10-14 15:52:51'),
(32, 'PRD-0002', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000014', '303303', 'POS sale', '2026-10-14 15:55:42'),
(33, 'PRD-0004', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000014', '303303', 'POS sale', '2026-10-14 15:55:42'),
(34, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000014', '303303', 'POS sale', '2026-10-14 15:55:42'),
(35, 'PRD-0002', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000015', '303303', 'POS sale', '2026-10-14 15:57:29'),
(36, 'PRD-0006', 'OUT', 'SALE', 3, NULL, NULL, NULL, NULL, 'SALE-000015', '303303', 'POS sale', '2026-10-14 15:57:29'),
(37, 'PRD-0002', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000016', '303303', 'POS sale', '2026-10-14 15:59:26'),
(38, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000017', '303303', 'POS sale', '2026-10-14 16:19:38'),
(39, 'PRD-0005', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000017', '303303', 'POS sale', '2026-10-14 16:19:38'),
(40, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000018', '303303', 'POS sale', '2026-10-14 16:30:07'),
(41, 'PRD-0007', 'IN', 'PURCHASE', 250, 'BATCH-20261022121500', '2027-10-22', 20.00, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-22 12:15:13'),
(42, 'PRD-0007', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000019', '303303', 'POS sale', '2026-10-22 12:15:47'),
(43, 'PRD-0008', 'IN', 'PURCHASE', 500, 'BATCH-20261022134927', '2026-11-03', 8.00, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-22 13:49:34'),
(44, 'PRD-0008', 'OUT', 'SALE', 25, NULL, NULL, NULL, NULL, 'SALE-000020', '303303', 'POS sale', '2026-10-22 13:51:32'),
(45, 'PRD-0008', 'OUT', 'SALE', 9, NULL, NULL, NULL, NULL, 'SALE-000021', '303303', 'POS sale', '2026-10-22 14:04:29'),
(46, 'PRD-0008', 'OUT', 'SALE', 7, NULL, NULL, NULL, NULL, 'SALE-000022', '303303', 'POS sale', '2026-10-22 14:07:35'),
(47, 'PRD-0004', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000023', '303303', 'POS sale', '2026-10-22 14:16:43'),
(48, 'PRD-0007', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000023', '303303', 'POS sale', '2026-10-22 14:16:44'),
(49, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000023', '303303', 'POS sale', '2026-10-22 14:16:44'),
(50, 'PRD-0006', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000023', '303303', 'POS sale', '2026-10-22 14:16:44'),
(51, 'PRD-0008', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(52, 'PRD-0007', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(53, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(54, 'PRD-0004', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(55, 'PRD-0005', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(56, 'PRD-0006', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000024', '303303', 'POS sale', '2026-10-22 14:25:21'),
(57, 'PRD-0007', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000025', '303303', 'POS sale', '2026-10-22 14:28:36'),
(58, 'PRD-0003', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000025', '303303', 'POS sale', '2026-10-22 14:28:36'),
(59, 'PRD-0008', 'OUT', 'SALE', 5, NULL, NULL, NULL, NULL, 'SALE-000025', '303303', 'POS sale', '2026-10-22 14:28:36'),
(60, 'PRD-0006', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000025', '303303', 'POS sale', '2026-10-22 14:28:36'),
(61, 'PRD-0005', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000025', '303303', 'POS sale', '2026-10-22 14:28:36'),
(62, 'PRD-0008', 'OUT', 'SALE', 3, NULL, NULL, NULL, NULL, 'SALE-000026', '303303', 'POS sale', '2026-10-22 14:34:15'),
(63, 'PRD-0005', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000026', '303303', 'POS sale', '2026-10-22 14:34:15'),
(64, 'PRD-0006', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000026', '303303', 'POS sale', '2026-10-22 14:34:15'),
(65, 'PRD-0003', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000026', '303303', 'POS sale', '2026-10-22 14:34:15'),
(66, 'PRD-0003', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000027', '303303', 'POS sale', '2026-10-22 14:40:41'),
(67, 'PRD-0007', 'OUT', 'SALE', 2, NULL, NULL, NULL, NULL, 'SALE-000027', '303303', 'POS sale', '2026-10-22 14:40:41'),
(68, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000028', '303303', 'POS sale', '2026-10-24 12:56:21'),
(69, 'PRD-0005', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000029', '303303', 'POS sale', '2026-10-24 12:58:10'),
(70, 'PRD-0003', 'OUT', 'SALE', 1, NULL, NULL, NULL, NULL, 'SALE-000030', '303303', 'POS sale', '2026-10-26 11:54:13'),
(71, 'PRD-0009', 'IN', 'PURCHASE', 30, NULL, '2026-11-25', 18.75, NULL, NULL, '303303', 'Opening stock on product creation', '2026-10-26 14:54:09'),
(72, 'PRD-0004', 'IN', 'ADJUSTMENT', 12, NULL, NULL, NULL, NULL, NULL, '303303', 'on stock', '2026-10-28 10:33:54'),
(73, 'PRD-0004', 'IN', 'PURCHASE', 13, 'BATCH-20261028160858', '2027-10-28', 125.00, NULL, NULL, '303303', 'damaged', '2026-10-28 16:09:06'),
(74, 'PRD-0004', 'IN', 'PURCHASE', 10, 'BATCH-20261028163051', '2027-10-28', 125.00, NULL, NULL, '303303', 'damaged', '2026-10-28 16:31:05'),
(75, 'PRD-0004', 'OUT', 'DAMAGED', 12, NULL, NULL, 125.00, NULL, NULL, '303303', 'Type: Other; Disposition: Pending; Details: damaged', '2026-10-28 16:31:39'),
(76, 'PRD-0007', 'OUT', 'EXPIRED', 130, NULL, NULL, 20.00, NULL, NULL, '303303', 'expired', '2026-10-28 16:36:03'),
(77, 'PRD-0007', 'OUT', 'EXPIRED', 100, NULL, NULL, 20.00, NULL, NULL, '303303', 'expired', '2026-10-28 16:39:08'),
(78, 'PRD-0007', 'OUT', 'EXPIRED', 9, NULL, NULL, 20.00, NULL, NULL, '303303', 'expired', '2026-10-28 16:39:54'),
(79, 'PRD-0008', 'OUT', 'EXPIRED', 400, NULL, NULL, 12.00, NULL, NULL, '303303', 'expired', '2026-10-28 16:40:54'),
(80, 'PRD-0008', 'OUT', 'DAMAGED', 45, NULL, NULL, 12.00, NULL, NULL, '303303', 'Type: Other; Disposition: Pending; Details: damaged', '2026-10-28 16:42:08'),
(81, 'PRD-0005', 'OUT', 'DAMAGED', 6, NULL, NULL, 110.00, NULL, NULL, '303303', 'Type: Other; Disposition: Pending; Details: leaking', '2026-10-28 16:43:33'),
(82, 'PRD-0010', 'IN', 'PURCHASE', 250, NULL, NULL, 25.00, NULL, NULL, 'ACC-001', 'Opening stock on product creation', '2026-10-29 09:47:02'),
(83, 'PRD-0003', 'IN', 'ADJUSTMENT', 250, NULL, NULL, NULL, NULL, NULL, 'ACC-001', 'bagong abot', '2026-10-29 09:47:48'),
(84, 'PRD-0010', 'OUT', 'SALE', 5, NULL, NULL, NULL, NULL, 'SALE-000031', 'ACC-001', 'POS sale', '2026-10-29 09:51:44'),
(85, 'PRD-0008', 'OUT', 'SALE', 5, NULL, NULL, NULL, NULL, 'SALE-000031', 'ACC-001', 'POS sale', '2026-10-29 09:51:44'),
(86, 'PRD-0010', 'OUT', 'SALE', 4, NULL, NULL, NULL, NULL, 'SALE-000032', 'ACC-001', 'POS sale', '2026-10-29 09:55:32');

-- --------------------------------------------------------

--
-- Table structure for table `tbl_supplier`
--

CREATE TABLE `tbl_supplier` (
  `supplier_id` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `supplier_name` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `contact_person` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `phone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `email` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `address` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

-- --------------------------------------------------------

--
-- Stand-in structure for view `vw_product_stock`
-- (See below for the actual view)
--
CREATE TABLE `vw_product_stock` (
`current_quantity` int
,`product_id` varchar(10)
);

-- --------------------------------------------------------

--
-- Structure for view `vw_product_stock`
--
DROP TABLE IF EXISTS `vw_product_stock`;

CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `vw_product_stock`  AS SELECT `tbl_stock`.`product_id` AS `product_id`, `tbl_stock`.`quantity` AS `current_quantity` FROM `tbl_stock` ;

--
-- Indexes for dumped tables
--

--
-- Indexes for table `tbl_account`
--
ALTER TABLE `tbl_account`
  ADD PRIMARY KEY (`acc_id`) USING BTREE,
  ADD UNIQUE KEY `uq_account_username` (`user_name`) USING BTREE;

--
-- Indexes for table `tbl_category`
--
ALTER TABLE `tbl_category`
  ADD PRIMARY KEY (`category_id`) USING BTREE,
  ADD UNIQUE KEY `uq_category_name` (`category_name`) USING BTREE;

--
-- Indexes for table `tbl_product`
--
ALTER TABLE `tbl_product`
  ADD PRIMARY KEY (`product_id`) USING BTREE,
  ADD UNIQUE KEY `uq_product_barcode` (`barcode`) USING BTREE,
  ADD KEY `fk_product_category` (`category_id`) USING BTREE,
  ADD KEY `fk_product_supplier` (`supplier_id`) USING BTREE;

--
-- Indexes for table `tbl_sale`
--
ALTER TABLE `tbl_sale`
  ADD PRIMARY KEY (`sale_id`) USING BTREE,
  ADD KEY `fk_sale_cashier` (`cashier_id`) USING BTREE,
  ADD KEY `idx_sale_created` (`created_at`) USING BTREE,
  ADD KEY `idx_sale_discount_approver` (`discount_approved_by`) USING BTREE;

--
-- Indexes for table `tbl_sale_item`
--
ALTER TABLE `tbl_sale_item`
  ADD PRIMARY KEY (`sale_item_id`) USING BTREE,
  ADD KEY `idx_saleitem_sale` (`sale_id`) USING BTREE,
  ADD KEY `idx_saleitem_product` (`product_id`) USING BTREE;

--
-- Indexes for table `tbl_stock`
--
ALTER TABLE `tbl_stock`
  ADD PRIMARY KEY (`stock_id`) USING BTREE,
  ADD UNIQUE KEY `uq_stock_product` (`product_id`) USING BTREE;

--
-- Indexes for table `tbl_stock_log`
--
ALTER TABLE `tbl_stock_log`
  ADD PRIMARY KEY (`log_id`) USING BTREE,
  ADD KEY `idx_stocklog_product` (`product_id`) USING BTREE,
  ADD KEY `idx_stocklog_expiry` (`expiry_date`) USING BTREE,
  ADD KEY `fk_stocklog_supplier` (`supplier_id`) USING BTREE,
  ADD KEY `fk_stocklog_account` (`performed_by`) USING BTREE;

--
-- Indexes for table `tbl_supplier`
--
ALTER TABLE `tbl_supplier`
  ADD PRIMARY KEY (`supplier_id`) USING BTREE;

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `tbl_sale_item`
--
ALTER TABLE `tbl_sale_item`
  MODIFY `sale_item_id` bigint UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=58;

--
-- AUTO_INCREMENT for table `tbl_stock`
--
ALTER TABLE `tbl_stock`
  MODIFY `stock_id` int UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=90;

--
-- AUTO_INCREMENT for table `tbl_stock_log`
--
ALTER TABLE `tbl_stock_log`
  MODIFY `log_id` bigint UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=87;

--
-- Constraints for dumped tables
--

--
-- Constraints for table `tbl_product`
--
ALTER TABLE `tbl_product`
  ADD CONSTRAINT `fk_product_category` FOREIGN KEY (`category_id`) REFERENCES `tbl_category` (`category_id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_product_supplier` FOREIGN KEY (`supplier_id`) REFERENCES `tbl_supplier` (`supplier_id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Constraints for table `tbl_sale`
--
ALTER TABLE `tbl_sale`
  ADD CONSTRAINT `fk_sale_cashier` FOREIGN KEY (`cashier_id`) REFERENCES `tbl_account` (`acc_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_sale_discount_approver` FOREIGN KEY (`discount_approved_by`) REFERENCES `tbl_account` (`acc_id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Constraints for table `tbl_sale_item`
--
ALTER TABLE `tbl_sale_item`
  ADD CONSTRAINT `fk_saleitem_product` FOREIGN KEY (`product_id`) REFERENCES `tbl_product` (`product_id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_saleitem_sale` FOREIGN KEY (`sale_id`) REFERENCES `tbl_sale` (`sale_id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Constraints for table `tbl_stock`
--
ALTER TABLE `tbl_stock`
  ADD CONSTRAINT `fk_stock_product` FOREIGN KEY (`product_id`) REFERENCES `tbl_product` (`product_id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Constraints for table `tbl_stock_log`
--
ALTER TABLE `tbl_stock_log`
  ADD CONSTRAINT `fk_stocklog_account` FOREIGN KEY (`performed_by`) REFERENCES `tbl_account` (`acc_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_stocklog_product` FOREIGN KEY (`product_id`) REFERENCES `tbl_product` (`product_id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_stocklog_supplier` FOREIGN KEY (`supplier_id`) REFERENCES `tbl_supplier` (`supplier_id`) ON DELETE SET NULL ON UPDATE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
