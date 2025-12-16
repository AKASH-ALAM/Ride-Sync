class Location {
  final double latitude;
  final double longitude;
  final String address;

  Location({
    required this.latitude,
    required this.longitude,
    required this.address,
  });

  factory Location.fromJson(Map<String, dynamic> json) {
    return Location(
      latitude: json['latitude']?.toDouble() ?? 0.0,
      longitude: json['longitude']?.toDouble() ?? 0.0,
      address: json['address'] ?? '',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'latitude': latitude,
      'longitude': longitude,
      'address': address,
    };
  }
}

class RideModel {
  final String id;
  final Map<String, dynamic>? rider;
  final Map<String, dynamic>? driver;
  final Location pickupLocation;
  final Location dropLocation;
  final String? vehicleType;
  final String status;
  final double fare;
  final double distance;
  final int estimatedTime;
  final DateTime? requestedAt;
  final DateTime? acceptedAt;
  final DateTime? completedAt;

  RideModel({
    required this.id,
    this.rider,
    this.driver,
    required this.pickupLocation,
    required this.dropLocation,
    this.vehicleType,
    required this.status,
    required this.fare,
    required this.distance,
    required this.estimatedTime,
    this.requestedAt,
    this.acceptedAt,
    this.completedAt,
  });

  factory RideModel.fromJson(Map<String, dynamic> json) {
    return RideModel(
      id: json['_id'] ?? json['id'] ?? '',
      rider: json['rider'],
      driver: json['driver'],
      pickupLocation: Location.fromJson(json['pickupLocation'] ?? {}),
      dropLocation: Location.fromJson(json['dropLocation'] ?? {}),
      vehicleType: json['vehicleType'],
      status: json['status'] ?? 'pending',
      fare: (json['fare'] ?? 0).toDouble(),
      distance: (json['distance'] ?? 0).toDouble(),
      estimatedTime: json['estimatedTime'] ?? 0,
      requestedAt: json['requestedAt'] != null
          ? DateTime.parse(json['requestedAt'])
          : null,
      acceptedAt: json['acceptedAt'] != null
          ? DateTime.parse(json['acceptedAt'])
          : null,
      completedAt: json['completedAt'] != null
          ? DateTime.parse(json['completedAt'])
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'rider': rider,
      'driver': driver,
      'pickupLocation': pickupLocation.toJson(),
      'dropLocation': dropLocation.toJson(),
      'vehicleType': vehicleType,
      'status': status,
      'fare': fare,
      'distance': distance,
      'estimatedTime': estimatedTime,
    };
  }

  String get statusText {
    switch (status) {
      case 'pending':
        return 'Waiting for driver';
      case 'accepted':
        return 'Driver on the way';
      case 'driver_arrived':
        return 'Driver arrived';
      case 'in_progress':
        return 'Ride in progress';
      case 'completed':
        return 'Completed';
      case 'cancelled':
        return 'Cancelled';
      default:
        return status;
    }
  }
}

