import 'package:flutter/foundation.dart';
import '../models/ride_model.dart';
import '../services/api_service.dart';

class RideProvider extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  List<RideModel> _availableRides = [];
  List<RideModel> _rideHistory = [];
  RideModel? _activeRide;
  bool _isLoading = false;
  String? _error;

  List<RideModel> get availableRides => _availableRides;
  List<RideModel> get rideHistory => _rideHistory;
  RideModel? get activeRide => _activeRide;
  bool get isLoading => _isLoading;
  String? get error => _error;

    Future<void> requestRide({
      required Map<String, dynamic> pickupLocation,
      required Map<String, dynamic> dropLocation,
      String? vehicleType,
    }) async {
      try {
        _isLoading = true;
        _error = null;
        notifyListeners();

        final response = await _apiService.requestRide(
          pickupLocation: pickupLocation,
          dropLocation: dropLocation,
          vehicleType: vehicleType,
        );

      _activeRide = RideModel.fromJson(response['ride']);
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      rethrow;
    }
  }

  Future<void> fetchAvailableRides() async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final rides = await _apiService.getAvailableRides();
      _availableRides = rides.map((r) => RideModel.fromJson(r)).toList();
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> acceptRide(String rideId) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.acceptRide(rideId);
      _activeRide = RideModel.fromJson(response['ride']);
      _availableRides.removeWhere((r) => r.id == rideId);
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      rethrow;
    }
  }

  Future<void> updateRideStatus(String rideId, String status) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final response = await _apiService.updateRideStatus(rideId, status);
      if (_activeRide?.id == rideId) {
        _activeRide = RideModel.fromJson(response['ride']);
      }
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      rethrow;
    }
  }

  Future<void> fetchRideHistory() async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final rides = await _apiService.getRideHistory();
      _rideHistory = rides.map((r) => RideModel.fromJson(r)).toList();
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> fetchActiveRide() async {
    try {
      final rideData = await _apiService.getActiveRide();
      if (rideData != null) {
        _activeRide = RideModel.fromJson(rideData);
      } else {
        _activeRide = null;
      }
      notifyListeners();
    } catch (e) {
      _activeRide = null;
      notifyListeners();
    }
  }

  void setActiveRide(RideModel? ride) {
    _activeRide = ride;
    notifyListeners();
  }

  void clearActiveRide() {
    _activeRide = null;
    notifyListeners();
  }

  Future<void> updateDriverAvailability({
    required bool isAvailable,
    Map<String, double>? currentLocation,
  }) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      await _apiService.updateDriverAvailability(
        isAvailable: isAvailable,
        currentLocation: currentLocation,
      );

      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
      rethrow;
    }
  }
}

