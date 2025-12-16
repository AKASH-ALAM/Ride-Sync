class TransactionModel {
  final String id;
  final String type; // 'credit' or 'debit'
  final double amount;
  final String description;
  final String category;
  final String status;
  final DateTime createdAt;
  final String? rideId;

  TransactionModel({
    required this.id,
    required this.type,
    required this.amount,
    required this.description,
    required this.category,
    required this.status,
    required this.createdAt,
    this.rideId,
  });

  factory TransactionModel.fromJson(Map<String, dynamic> json) {
    return TransactionModel(
      id: json['_id'] ?? json['id'] ?? '',
      type: json['type'] ?? '',
      amount: (json['amount'] ?? 0).toDouble(),
      description: json['description'] ?? '',
      category: json['category'] ?? 'other',
      status: json['status'] ?? 'completed',
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'])
          : DateTime.now(),
      rideId: json['ride']?['_id'] ?? json['ride'],
    );
  }

  bool get isCredit => type == 'credit';
  bool get isDebit => type == 'debit';
}

