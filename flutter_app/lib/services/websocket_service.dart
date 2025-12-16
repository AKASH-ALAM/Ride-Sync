import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/web_socket_channel.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../config/app_config.dart';

class WebSocketService extends ChangeNotifier {
  WebSocketChannel? _channel;
  bool _isConnected = false;
  String? _userId;
  String? _userType;

  bool get isConnected => _isConnected;

  static String get wsUrl => AppConfig.wsUrl;

  Function(Map<String, dynamic>)? onNewRideRequest;
  Function(Map<String, dynamic>)? onRideAccepted;
  Function(Map<String, dynamic>)? onRideStatusUpdated;

  Future<void> connect() async {
    if (_isConnected) return;

    try {
      _channel = WebSocketChannel.connect(Uri.parse(WebSocketService.wsUrl));

      _channel!.stream.listen(
        (message) {
          _handleMessage(message);
        },
        onError: (error) {
          debugPrint('WebSocket error: $error');
          _isConnected = false;
          notifyListeners();
        },
        onDone: () {
          debugPrint('WebSocket connection closed');
          _isConnected = false;
          notifyListeners();
        },
      );

      // Authenticate after connection
      await _authenticate();
    } catch (e) {
      debugPrint('Failed to connect WebSocket: $e');
      _isConnected = false;
      notifyListeners();
    }
  }

  Future<void> _authenticate() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('token');
    final userId = prefs.getString('userId');
    final userType = prefs.getString('userType');

    if (token != null && userId != null && userType != null) {
      _userId = userId;
      _userType = userType;

      _channel?.sink.add(jsonEncode({
        'type': 'authenticate',
        'userId': userId,
        'userType': userType,
      }));

      _isConnected = true;
      notifyListeners();
    }
  }

  void _handleMessage(dynamic message) {
    try {
      final data = jsonDecode(message.toString());

      switch (data['type']) {
        case 'authenticated':
          _isConnected = true;
          notifyListeners();
          debugPrint('WebSocket authenticated');
          break;

        case 'new_ride_request':
          if (onNewRideRequest != null) {
            onNewRideRequest!(data);
          }
          break;

        case 'ride_accepted':
          if (onRideAccepted != null) {
            onRideAccepted!(data);
          }
          break;

        case 'ride_status_updated':
          if (onRideStatusUpdated != null) {
            onRideStatusUpdated!(data);
          }
          break;

        case 'pong':
          // Keep-alive response
          break;

        default:
          debugPrint('Unknown WebSocket message type: ${data['type']}');
      }
    } catch (e) {
      debugPrint('Error parsing WebSocket message: $e');
    }
  }

  void sendMessage(Map<String, dynamic> message) {
    if (_channel != null && _isConnected) {
      _channel!.sink.add(jsonEncode(message));
    }
  }

  void ping() {
    sendMessage({'type': 'ping'});
  }

  void disconnect() {
    _channel?.sink.close();
    _channel = null;
    _isConnected = false;
    notifyListeners();
  }

  @override
  void dispose() {
    disconnect();
    super.dispose();
  }
}

