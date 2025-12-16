import 'package:flutter/foundation.dart';
import '../models/transaction_model.dart';
import '../services/api_service.dart';

class WalletProvider extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  double _balance = 0.0;
  double? _totalEarnings; // For drivers
  List<TransactionModel> _transactions = [];
  bool _isLoading = false;
  String? _error;

  double get balance => _balance;
  double? get totalEarnings => _totalEarnings;
  List<TransactionModel> get transactions => _transactions;
  bool get isLoading => _isLoading;
  String? get error => _error;

  Future<void> fetchBalance() async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final data = await _apiService.getWalletBalance();
      _balance = (data['balance'] ?? 0).toDouble();
      _totalEarnings = data['totalEarnings'] != null
          ? (data['totalEarnings'] as num).toDouble()
          : null;

      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> topUpWallet(double amount, {String paymentMethod = 'Demo Payment'}) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final data = await _apiService.topUpWallet(
        amount: amount,
        paymentMethod: paymentMethod,
      );

      _balance = (data['balance'] ?? _balance).toDouble();
      await fetchTransactions(); // Refresh transactions

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

  Future<void> fetchTransactions({int limit = 50, int page = 1}) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final transactionsData = await _apiService.getTransactions(
        limit: limit,
        page: page,
      );

      _transactions = transactionsData
          .map((t) => TransactionModel.fromJson(t))
          .toList();

      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> withdrawMoney(double amount, {String accountDetails = 'Bank Account'}) async {
    try {
      _isLoading = true;
      _error = null;
      notifyListeners();

      final data = await _apiService.withdrawMoney(
        amount: amount,
        accountDetails: accountDetails,
      );

      _balance = (data['balance'] ?? _balance).toDouble();
      await fetchTransactions(); // Refresh transactions

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
}

