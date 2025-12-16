# Ride Sync - Ride-Sharing Application

A complete ride-sharing application inspired by Uber and Pathao, built with Flutter (mobile app) and Node.js/Express (backend server).

## 🚀 Project Overview

This is a full-stack ride-sharing application with:
- **Mobile App**: Built with Flutter (Dart) - supports Android
- **Backend Server**: Built with Express.js and Node.js
- **Database**: MongoDB with Mongoose
- **Real-time Communication**: WebSocket for live updates

## 📁 Project Structure

```
ride-sync/
├── backend/              # Node.js/Express backend server
│   ├── config/          # Database configuration
│   ├── models/          # Mongoose models (Rider, Driver, Ride)
│   ├── routes/          # API routes (auth, rides)
│   ├── middleware/      # Authentication middleware
│   ├── server.js        # Main server file
│   └── package.json     # Backend dependencies
│
└── flutter_app/         # Flutter mobile application
    ├── lib/             # Dart source code
    │   ├── models/      # Data models
    │   ├── services/    # API & WebSocket services
    │   ├── providers/   # State management
    │   └── screens/     # UI screens
    ├── android/         # Android configuration
    └── pubspec.yaml     # Flutter dependencies
```

## ✨ Features

### Rider Features
- ✅ User registration and authentication
- ✅ Request rides with pickup and drop locations
- ✅ Real-time ride status updates via WebSocket
- ✅ View ride history
- ✅ Beautiful, modern UI

### Driver Features
- ✅ Driver registration and authentication
- ✅ View available ride requests
- ✅ Accept ride requests
- ✅ Update ride status (Arrived, In Progress, Completed)
- ✅ Toggle availability status
- ✅ Real-time ride notifications

## 🛠️ Tech Stack

### Backend
- **Runtime**: Node.js
- **Framework**: Express.js
- **Database**: MongoDB with Mongoose
- **Real-time**: WebSocket (ws library)
- **Authentication**: JWT (JSON Web Tokens)
- **Password Hashing**: bcryptjs

### Mobile App
- **Framework**: Flutter (Dart)
- **State Management**: Provider
- **HTTP Client**: http package
- **WebSocket**: web_socket_channel
- **Location**: geolocator, geocoding
- **Storage**: shared_preferences
- **UI**: Material Design 3 with Google Fonts

## 📋 Prerequisites

### For Backend
- Node.js (v14 or higher)
- MongoDB (local or cloud instance)
- npm or yarn

### For Mobile App
- Flutter SDK (v3.0.0 or higher)
- Android Studio (for Android development)
- Android SDK
- Physical Android device or emulator

## 🚀 Quick Start

### 1. Backend Setup

```bash
# Navigate to backend directory
cd backend

# Install dependencies
npm install

# Create .env file (copy from .env.example)
cp .env.example .env

# Edit .env file with your configuration
# PORT=3000
# MONGODB_URI=mongodb://localhost:27017/ride-sync
# JWT_SECRET=your-secret-key

# Start MongoDB (if running locally)
# Make sure MongoDB is running on your system

# Start the server
npm start
# or for development with auto-reload
npm run dev
```

The backend server will run on `http://localhost:3000`

### 2. Flutter App Setup

```bash
# Navigate to Flutter app directory
cd flutter_app

# Install dependencies
flutter pub get

# Update backend URL in:
# - lib/services/api_service.dart
# - lib/services/websocket_service.dart
#
# For Android Emulator: http://10.0.2.2:3000
# For Physical Device: http://YOUR_COMPUTER_IP:3000

# Run the app
flutter run
```

### 3. Build Release APK

```bash
# Navigate to Flutter app directory
cd flutter_app

# Build release APK
flutter build apk --release

# The APK will be at:
# build/app/outputs/flutter-apk/app-release.apk
```

**Install on Physical Device:**
1. Enable USB Debugging on your Android device
2. Connect device via USB
3. Run: `adb install build/app/outputs/flutter-apk/app-release.apk`

Or transfer the APK file to your device and install manually.

## 📱 API Endpoints

### Authentication
- `POST /api/auth/rider/register` - Register rider
- `POST /api/auth/rider/login` - Login rider
- `POST /api/auth/driver/register` - Register driver
- `POST /api/auth/driver/login` - Login driver
- `GET /api/auth/me` - Get current user

### Rides
- `POST /api/rides/request` - Request a ride (Rider)
- `GET /api/rides/available` - Get available rides (Driver)
- `POST /api/rides/:rideId/accept` - Accept ride (Driver)
- `PATCH /api/rides/:rideId/status` - Update ride status
- `GET /api/rides/:rideId` - Get ride details
- `GET /api/rides/history/all` - Get ride history
- `GET /api/rides/active/current` - Get active ride
- `PATCH /api/rides/driver/availability` - Update driver availability

## 🔌 WebSocket Events

### Client → Server
- `authenticate` - Authenticate WebSocket connection
- `ping` - Keep connection alive

### Server → Client
- `authenticated` - Authentication confirmation
- `new_ride_request` - New ride available (Driver)
- `ride_accepted` - Ride accepted (Rider)
- `ride_status_updated` - Ride status changed
- `pong` - Response to ping

## 🎨 UI Features

- Modern Material Design 3 interface
- Beautiful gradient backgrounds
- Smooth animations and transitions
- Intuitive navigation
- Real-time status updates
- Responsive layouts

## 📝 Configuration

### Backend Configuration
Edit `backend/.env`:
```
PORT=3000
MONGODB_URI=mongodb://localhost:27017/ride-sync
JWT_SECRET=your-secret-key-change-this-in-production
```

### Flutter App Configuration
Update backend URLs in:
- `lib/services/api_service.dart` - `baseUrl`
- `lib/services/websocket_service.dart` - `wsUrl`

## 🐛 Troubleshooting

### Backend Issues
- **MongoDB Connection Error**: Ensure MongoDB is running
- **Port Already in Use**: Change PORT in .env file
- **JWT Errors**: Check JWT_SECRET in .env

### Flutter App Issues
- **Connection Error**: Verify backend URL and server status
- **Location Permission**: Enable location permissions in device settings
- **Build Errors**: Run `flutter clean` and `flutter pub get`

## 📚 Documentation

- [Backend README](backend/README.md) - Detailed backend documentation
- [Flutter App README](flutter_app/README.md) - Detailed Flutter app documentation

## 🎓 Academic Project

This project is designed for university academic purposes with:
- Clean, beginner-friendly code
- Comprehensive documentation
- Full-stack implementation
- Real-time features
- Production-ready structure

## 📄 License

This project is for educational/academic purposes.

## 👨‍💻 Development

### Running in Development Mode

**Backend:**
```bash
cd backend
npm run dev  # Uses nodemon for auto-reload
```

**Flutter:**
```bash
cd flutter_app
flutter run  # Hot reload enabled
```

## 🔐 Security Notes

- Change JWT_SECRET in production
- Use HTTPS in production
- Implement proper input validation
- Add rate limiting for API endpoints
- Secure MongoDB connection in production

---

**Built with ❤️ for academic purposes**

