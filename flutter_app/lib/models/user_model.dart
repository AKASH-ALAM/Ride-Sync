class UserModel {
  final String id;
  final String name;
  final String email;
  final String phone;
  final String type; // 'rider'/'passenger' or 'driver'
  final String? vehicleType;
  final String? vehicleNumber;
  final bool? isAvailable;

  UserModel({
    required this.id,
    required this.name,
    required this.email,
    required this.phone,
    required this.type,
    this.vehicleType,
    this.vehicleNumber,
    this.isAvailable,
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id'] ?? json['_id'] ?? '',
      name: json['name'] ?? '',
      email: json['email'] ?? '',
      phone: json['phone'] ?? '',
      type: json['type'] ?? '',
      vehicleType: json['vehicleType'],
      vehicleNumber: json['vehicleNumber'],
      isAvailable: json['isAvailable'],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'email': email,
      'phone': phone,
      'type': type,
      'vehicleType': vehicleType,
      'vehicleNumber': vehicleNumber,
      'isAvailable': isAvailable,
    };
  }
}

