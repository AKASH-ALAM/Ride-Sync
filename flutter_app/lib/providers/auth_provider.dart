import 'package:flutter/foundation.dart';
import '../models/user_model.dart';
import '../services/api_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AuthProvider extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  UserModel? _user;
  bool _isLoading = false;
  String? _error;

  UserModel? get user => _user;
  bool get isLoading => _isLoading;
  String? get error => _error;
  bool get isAuthenticated => _user != null;

  Future<void> checkAuth() async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final prefs = await SharedPreferences.getInstance();
      final token = prefs.getString('token');

      if (token != null) {
        try {
          final response = await _apiService.getCurrentUser();
          _user = UserModel.fromJson(response['user']);
          await prefs.setString('userId', _user!.id);
          await prefs.setString('userType', _user!.type);
        } catch (e) {
          // If token is invalid or backend is not available, clear it
          _error = e.toString();
          await logout();
        }
      }
    } catch (e) {
      _error = e.toString();
      // Don't throw, just clear user state
      _user = null;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> riderRegister({
    required String name,
    required String email,
    required String phone,
    required String password,
  }) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.riderRegister(
        name: name,
        email: email,
        phone: phone,
        password: password,
      );

      _user = UserModel.fromJson(response['user']);

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('userId', _user!.id);
      await prefs.setString('userType', _user!.type);

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      return false;
    }
  }

  Future<bool> riderLogin({
    required String email,
    required String password,
  }) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.riderLogin(
        email: email,
        password: password,
      );

      _user = UserModel.fromJson(response['user']);

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('userId', _user!.id);
      await prefs.setString('userType', _user!.type);

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      return false;
    }
  }

  Future<bool> driverRegister({
    required String name,
    required String email,
    required String phone,
    required String password,
    required String vehicleType,
    required String vehicleNumber,
    required String licenseNumber,
  }) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.driverRegister(
        name: name,
        email: email,
        phone: phone,
        password: password,
        vehicleType: vehicleType,
        vehicleNumber: vehicleNumber,
        licenseNumber: licenseNumber,
      );

      _user = UserModel.fromJson(response['user']);

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('userId', _user!.id);
      await prefs.setString('userType', _user!.type);

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      return false;
    }
  }

  Future<bool> driverLogin({
    required String email,
    required String password,
  }) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.driverLogin(
        email: email,
        password: password,
      );

      _user = UserModel.fromJson(response['user']);

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('userId', _user!.id);
      await prefs.setString('userType', _user!.type);

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      return false;
    }
  }

    Future<bool> updateProfile({
      String? name,
      String? phone,
      String? vehicleType,
      String? vehicleNumber,
    }) async {
      try {
        _isLoading = true;
        _error = null;
        notifyListeners();

        final response = await _apiService.updateProfile(
          name: name,
          phone: phone,
          vehicleType: vehicleType,
          vehicleNumber: vehicleNumber,
        );

        _user = UserModel.fromJson(response['user']);

        _isLoading = false;
        notifyListeners();
        return true;
      } catch (e) {
        _error = e.toString();
        _isLoading = false;
        notifyListeners();
        return false;
      }
    }

    Future<void> logout() async {
      await _apiService.logout();
      _user = null;
      _error = null;

      final prefs = await SharedPreferences.getInstance();
      await prefs.remove('userId');
      await prefs.remove('userType');

      notifyListeners();
    }
  }

