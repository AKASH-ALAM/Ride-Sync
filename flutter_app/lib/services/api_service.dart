import 'dart:convert';
import 'dart:async';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../config/app_config.dart';

class ApiService {
  static String get baseUrl => AppConfig.apiBaseUrl;

  Future<String?> _getToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('token');
  }

  Future<Map<String, String>> _getHeaders() async {
    final token = await _getToken();
    return {
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  // Authentication
  Future<Map<String, dynamic>> riderRegister({
    required String name,
    required String email,
    required String phone,
    required String password,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/rider/register'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'name': name,
        'email': email,
        'phone': phone,
        'password': password,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 201) {
      await _saveToken(data['token']);
      return data;
    } else {
      throw Exception(data['message'] ?? 'Registration failed');
    }
  }

  Future<Map<String, dynamic>> riderLogin({
    required String email,
    required String password,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/rider/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'email': email,
        'password': password,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      await _saveToken(data['token']);
      return data;
    } else {
      throw Exception(data['message'] ?? 'Login failed');
    }
  }

  Future<Map<String, dynamic>> driverRegister({
    required String name,
    required String email,
    required String phone,
    required String password,
    required String vehicleType,
    required String vehicleNumber,
    required String licenseNumber,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/driver/register'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'name': name,
        'email': email,
        'phone': phone,
        'password': password,
        'vehicleType': vehicleType,
        'vehicleNumber': vehicleNumber,
        'licenseNumber': licenseNumber,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 201) {
      await _saveToken(data['token']);
      return data;
    } else {
      throw Exception(data['message'] ?? 'Registration failed');
    }
  }

  Future<Map<String, dynamic>> driverLogin({
    required String email,
    required String password,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/driver/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'email': email,
        'password': password,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      await _saveToken(data['token']);
      return data;
    } else {
      throw Exception(data['message'] ?? 'Login failed');
    }
  }

  Future<Map<String, dynamic>> getCurrentUser() async {
    final response = await http.get(
      Uri.parse('$baseUrl/auth/me'),
      headers: await _getHeaders(),
    ).timeout(
      const Duration(seconds: 5),
      onTimeout: () {
        throw TimeoutException('Connection timeout. Please check if the backend is running.');
      },
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to get user');
    }
  }

  // Rides
  Future<Map<String, dynamic>> requestRide({
    required Map<String, dynamic> pickupLocation,
    required Map<String, dynamic> dropLocation,
    String? vehicleType,
  }) async {
    final body = <String, dynamic>{
      'pickupLocation': pickupLocation,
      'dropLocation': dropLocation,
    };
    if (vehicleType != null) {
      body['vehicleType'] = vehicleType;
    }
    
    final response = await http.post(
      Uri.parse('$baseUrl/rides/request'),
      headers: await _getHeaders(),
      body: jsonEncode(body),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 201) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to request ride');
    }
  }

  Future<List<dynamic>> getAvailableRides() async {
    final response = await http.get(
      Uri.parse('$baseUrl/rides/available'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data['rides'] ?? [];
    } else {
      throw Exception(data['message'] ?? 'Failed to get rides');
    }
  }

  Future<Map<String, dynamic>> acceptRide(String rideId) async {
    final response = await http.post(
      Uri.parse('$baseUrl/rides/$rideId/accept'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to accept ride');
    }
  }

  Future<Map<String, dynamic>> updateRideStatus(
    String rideId,
    String status,
  ) async {
    final response = await http.patch(
      Uri.parse('$baseUrl/rides/$rideId/status'),
      headers: await _getHeaders(),
      body: jsonEncode({'status': status}),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to update status');
    }
  }

  Future<Map<String, dynamic>> getRideDetails(String rideId) async {
    final response = await http.get(
      Uri.parse('$baseUrl/rides/$rideId'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to get ride details');
    }
  }

  Future<List<dynamic>> getRideHistory() async {
    final response = await http.get(
      Uri.parse('$baseUrl/rides/history/all'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data['rides'] ?? [];
    } else {
      throw Exception(data['message'] ?? 'Failed to get ride history');
    }
  }

  Future<Map<String, dynamic>?> getActiveRide() async {
    final response = await http.get(
      Uri.parse('$baseUrl/rides/active/current'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data['ride'];
    } else {
      return null;
    }
  }

  Future<void> updateDriverAvailability({
    required bool isAvailable,
    Map<String, double>? currentLocation,
  }) async {
    final body = <String, dynamic>{'isAvailable': isAvailable};
    if (currentLocation != null) {
      body['currentLocation'] = currentLocation;
    }

    await http.patch(
      Uri.parse('$baseUrl/rides/driver/availability'),
      headers: await _getHeaders(),
      body: jsonEncode(body),
    );
  }

  Future<Map<String, dynamic>> updateProfile({
    String? name,
    String? phone,
    String? vehicleType,
    String? vehicleNumber,
  }) async {
    final body = <String, dynamic>{};
    if (name != null) body['name'] = name;
    if (phone != null) body['phone'] = phone;
    if (vehicleType != null) body['vehicleType'] = vehicleType;
    if (vehicleNumber != null) body['vehicleNumber'] = vehicleNumber;

    final response = await http.patch(
      Uri.parse('$baseUrl/auth/profile'),
      headers: await _getHeaders(),
      body: jsonEncode(body),
    ).timeout(const Duration(seconds: 10));

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to update profile');
    }
  }

  Future<void> _saveToken(String token) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('token', token);
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('token');
  }

  // Wallet methods
  Future<Map<String, dynamic>> getWalletBalance() async {
    final response = await http.get(
      Uri.parse('$baseUrl/wallet/balance'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to get wallet balance');
    }
  }

  Future<Map<String, dynamic>> topUpWallet({
    required double amount,
    String paymentMethod = 'Demo Payment',
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/wallet/topup'),
      headers: await _getHeaders(),
      body: jsonEncode({
        'amount': amount,
        'paymentMethod': paymentMethod,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to add money');
    }
  }

  Future<List<dynamic>> getTransactions({int limit = 50, int page = 1}) async {
    final response = await http.get(
      Uri.parse('$baseUrl/wallet/transactions?limit=$limit&page=$page'),
      headers: await _getHeaders(),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data['transactions'] ?? [];
    } else {
      throw Exception(data['message'] ?? 'Failed to get transactions');
    }
  }

  Future<Map<String, dynamic>> withdrawMoney({
    required double amount,
    String accountDetails = 'Bank Account',
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/wallet/withdraw'),
      headers: await _getHeaders(),
      body: jsonEncode({
        'amount': amount,
        'accountDetails': accountDetails,
      }),
    );

    final data = jsonDecode(response.body);
    if (response.statusCode == 200) {
      return data;
    } else {
      throw Exception(data['message'] ?? 'Failed to withdraw money');
    }
  }
}

