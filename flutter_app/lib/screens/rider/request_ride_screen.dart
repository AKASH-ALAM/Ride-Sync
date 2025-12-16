import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:geolocator/geolocator.dart';
import 'package:geocoding/geocoding.dart';
import 'package:flutter/foundation.dart' show kIsWeb;
import '../../providers/ride_provider.dart';
import '../../providers/wallet_provider.dart';
import '../../services/websocket_service.dart';
import '../../widgets/location_tile.dart';
import '../../widgets/vehicle_selector.dart';
import '../../widgets/info_card.dart';
import '../../widgets/primary_button.dart';
import 'ride_status_screen.dart';
import '../wallet/wallet_screen.dart';

class RequestRideScreen extends StatefulWidget {
  const RequestRideScreen({super.key});

  @override
  State<RequestRideScreen> createState() => _RequestRideScreenState();
}

class _RequestRideScreenState extends State<RequestRideScreen> {
  final _pickupController = TextEditingController();
  final _dropController = TextEditingController();
  Map<String, dynamic>? _pickupLocation;
  Map<String, dynamic>? _dropLocation;
  String? _selectedVehicleType;
  bool _isLoading = false;
  double? _calculatedDistance;
  int? _estimatedTime;
  double? _estimatedFare;

  @override
  void dispose() {
    _pickupController.dispose();
    _dropController.dispose();
    super.dispose();
  }

  // Calculate distance using Haversine formula
  double _calculateDistance(double lat1, double lon1, double lat2, double lon2) {
    const R = 6371.0; // Radius of Earth in km
    final dLat = (lat2 - lat1) * pi / 180;
    final dLon = (lon2 - lon1) * pi / 180;
    final a = 
      sin(dLat / 2) * sin(dLat / 2) +
      cos(lat1 * pi / 180) * cos(lat2 * pi / 180) *
      sin(dLon / 2) * sin(dLon / 2);
    final c = 2 * atan2(sqrt(a), sqrt(1 - a));
    return R * c;
  }

  void _updateEstimates() {
    if (_pickupLocation != null && _dropLocation != null) {
      setState(() {
        // Calculate distance
        _calculatedDistance = _calculateDistance(
          _pickupLocation!['latitude'],
          _pickupLocation!['longitude'],
          _dropLocation!['latitude'],
          _dropLocation!['longitude'],
        );

        // Estimate time (3 minutes per km)
        _estimatedTime = (_calculatedDistance! * 3).round();

        // Calculate fare based on vehicle type
        double baseFare = 50; // Base fare
        double perKmRate = 15; // Default rate per km
        
        if (_selectedVehicleType == 'Bike') {
          baseFare = 30;
          perKmRate = 10;
        } else if (_selectedVehicleType == 'Car') {
          baseFare = 60;
          perKmRate = 20;
        } else if (_selectedVehicleType == 'Auto') {
          baseFare = 40;
          perKmRate = 12;
        }

        _estimatedFare = baseFare + (_calculatedDistance! * perKmRate);
      });
    }
  }

  Future<void> _getCurrentLocation(bool isPickup) async {
    setState(() {
      _isLoading = true;
    });

    try {
      if (kIsWeb) {
        // Use HTML5 Geolocation API for web
        await _getWebLocation(isPickup);
        return;
      }

      bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        _showError('Location services are disabled');
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          _showError('Location permissions are denied');
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        _showError('Location permissions are permanently denied');
        return;
      }

      Position position = await Geolocator.getCurrentPosition();
      List<Placemark> placemarks = await placemarkFromCoordinates(
        position.latitude,
        position.longitude,
      );

      String address = 'Unknown Location';
      if (placemarks.isNotEmpty) {
        final place = placemarks[0];
        List<String> addressParts = [];
        if (place.street != null && place.street!.isNotEmpty) addressParts.add(place.street!);
        if (place.subLocality != null && place.subLocality!.isNotEmpty) addressParts.add(place.subLocality!);
        if (place.locality != null && place.locality!.isNotEmpty) addressParts.add(place.locality!);
        if (place.subAdministrativeArea != null && place.subAdministrativeArea!.isNotEmpty) addressParts.add(place.subAdministrativeArea!);
        if (place.administrativeArea != null && place.administrativeArea!.isNotEmpty) addressParts.add(place.administrativeArea!);
        if (place.country != null && place.country!.isNotEmpty) addressParts.add(place.country!);
        
        address = addressParts.isNotEmpty 
            ? addressParts.join(', ')
            : '${place.locality ?? ''}, ${place.country ?? ''}'.replaceAll(RegExp(r'^,\s*|,\s*$'), '');
      }
      
      if (address == 'Unknown Location' || address.isEmpty) {
        address = '${position.latitude.toStringAsFixed(6)}, ${position.longitude.toStringAsFixed(6)}';
      }

      final location = {
        'latitude': position.latitude,
        'longitude': position.longitude,
        'address': address,
      };

      if (isPickup) {
        _pickupLocation = location;
        _pickupController.text = address;
      } else {
        _dropLocation = location;
        _dropController.text = address;
      }
      _updateEstimates();
    } catch (e) {
      _showError('Error getting location: $e');
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  Future<void> _getWebLocation(bool isPickup) async {
    try {
      // Request location permission and get position
      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          _showError('Location permission denied. Please allow location access.');
          _showLocationDialog(isPickup);
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        _showError('Location permission permanently denied. Please enter location manually.');
        _showLocationDialog(isPickup);
        return;
      }

      Position position = await Geolocator.getCurrentPosition(
        desiredAccuracy: LocationAccuracy.high,
      );

      // Reverse geocode to get address
      List<Placemark> placemarks = await placemarkFromCoordinates(
        position.latitude,
        position.longitude,
      );

      String address = 'Unknown Location';
      if (placemarks.isNotEmpty) {
        final place = placemarks[0];
        List<String> addressParts = [];
        if (place.street != null && place.street!.isNotEmpty) addressParts.add(place.street!);
        if (place.subLocality != null && place.subLocality!.isNotEmpty) addressParts.add(place.subLocality!);
        if (place.locality != null && place.locality!.isNotEmpty) addressParts.add(place.locality!);
        if (place.subAdministrativeArea != null && place.subAdministrativeArea!.isNotEmpty) addressParts.add(place.subAdministrativeArea!);
        if (place.administrativeArea != null && place.administrativeArea!.isNotEmpty) addressParts.add(place.administrativeArea!);
        if (place.country != null && place.country!.isNotEmpty) addressParts.add(place.country!);
        
        address = addressParts.isNotEmpty 
            ? addressParts.join(', ')
            : '${place.locality ?? ''}, ${place.country ?? ''}'.replaceAll(RegExp(r'^,\s*|,\s*$'), '');
      }
      
      if (address == 'Unknown Location' || address.isEmpty) {
        address = '${position.latitude.toStringAsFixed(6)}, ${position.longitude.toStringAsFixed(6)}';
      }

      final location = {
        'latitude': position.latitude,
        'longitude': position.longitude,
        'address': address,
      };

      if (isPickup) {
        _pickupLocation = location;
        _pickupController.text = address;
      } else {
        _dropLocation = location;
        _dropController.text = address;
      }
      _updateEstimates();
    } catch (e) {
      _showError('Failed to get location: $e\nPlease use the edit button to enter location manually');
      _showLocationDialog(isPickup);
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: Colors.red,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        duration: const Duration(seconds: 4),
      ),
    );
  }

  Future<void> _showLocationDialog(bool isPickup) async {
    final addressController = TextEditingController(
      text: isPickup 
          ? (_pickupLocation?['address'] as String? ?? '')
          : (_dropLocation?['address'] as String? ?? ''),
    );
    bool isLoadingLocation = false;
    bool isGeocoding = false;
    Map<String, double>? geocodedCoordinates;

    await showDialog(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(20),
          ),
          title: Row(
            children: [
              Icon(
                isPickup ? Icons.location_on_rounded : Icons.location_on_rounded,
                color: isPickup ? Colors.green : Colors.red,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  isPickup ? 'Pickup Location' : 'Drop Location',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Use Current Location Button (only for pickup)
                if (isPickup) ...[
                  Container(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [
                          const Color(0xFF1A1A2E).withOpacity(0.1),
                          const Color(0xFF16213E).withOpacity(0.1),
                        ],
                      ),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(
                        color: const Color(0xFF1A1A2E).withOpacity(0.2),
                      ),
                    ),
                    child: TextButton.icon(
                    onPressed: isLoadingLocation
                        ? null
                        : () async {
                            setDialogState(() {
                              isLoadingLocation = true;
                            });
                            try {
                              Position position = await Geolocator.getCurrentPosition(
                                desiredAccuracy: LocationAccuracy.high,
                              );
                              
                              // Try to get address from coordinates
                              try {
                                List<Placemark> placemarks = await placemarkFromCoordinates(
                                  position.latitude,
                                  position.longitude,
                                );
                                if (placemarks.isNotEmpty) {
                                  final place = placemarks[0];
                                  List<String> addressParts = [];
                                  if (place.street != null && place.street!.isNotEmpty) addressParts.add(place.street!);
                                  if (place.subLocality != null && place.subLocality!.isNotEmpty) addressParts.add(place.subLocality!);
                                  if (place.locality != null && place.locality!.isNotEmpty) addressParts.add(place.locality!);
                                  if (place.subAdministrativeArea != null && place.subAdministrativeArea!.isNotEmpty) addressParts.add(place.subAdministrativeArea!);
                                  if (place.administrativeArea != null && place.administrativeArea!.isNotEmpty) addressParts.add(place.administrativeArea!);
                                  if (place.country != null && place.country!.isNotEmpty) addressParts.add(place.country!);
                                  
                                  final formattedAddress = addressParts.isNotEmpty 
                                      ? addressParts.join(', ')
                                      : '${place.locality ?? ''}, ${place.country ?? ''}'.replaceAll(RegExp(r'^,\s*|,\s*$'), '');
                                  
                                  if (formattedAddress.isNotEmpty && formattedAddress != ', ') {
                                    addressController.text = formattedAddress;
                                    geocodedCoordinates = {
                                      'latitude': position.latitude,
                                      'longitude': position.longitude,
                                    };
                                  } else {
                                    addressController.text = 'Current Location';
                                    geocodedCoordinates = {
                                      'latitude': position.latitude,
                                      'longitude': position.longitude,
                                    };
                                  }
                                } else {
                                  addressController.text = 'Current Location';
                                  geocodedCoordinates = {
                                    'latitude': position.latitude,
                                    'longitude': position.longitude,
                                  };
                                }
                              } catch (e) {
                                // Address lookup failed, but we have coordinates
                                addressController.text = 'Current Location';
                                geocodedCoordinates = {
                                  'latitude': position.latitude,
                                  'longitude': position.longitude,
                                };
                              }
                            } catch (e) {
                              if (context.mounted) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text('Failed to get location: $e'),
                                    backgroundColor: Colors.red,
                                  ),
                                );
                              }
                            } finally {
                              setDialogState(() {
                                isLoadingLocation = false;
                              });
                            }
                          },
                    icon: isLoadingLocation
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.my_location_rounded),
                    label: Text(isLoadingLocation ? 'Getting location...' : 'Use Current Location'),
                    style: TextButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      foregroundColor: const Color(0xFF1A1A2E),
                    ),
                  ),
                  ),
                  const SizedBox(height: 20),
                  const Divider(),
                  const SizedBox(height: 16),
                ],
                Text(
                  isPickup ? 'Or enter address manually:' : 'Enter destination address:',
                  style: TextStyle(
                    color: Colors.grey[600],
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: addressController,
                  decoration: InputDecoration(
                    labelText: isPickup ? 'Enter Pickup Address' : 'Enter Destination Address',
                    hintText: isPickup 
                        ? 'e.g., Times Square, New York or 123 Main St, City, Country'
                        : 'e.g., Mirpur 1, Dhaka, Bangladesh or Airport Road, City',
                    prefixIcon: Icon(isPickup ? Icons.location_on_rounded : Icons.place_rounded),
                    suffixIcon: addressController.text.isNotEmpty
                        ? IconButton(
                            icon: const Icon(Icons.clear_rounded),
                            onPressed: () {
                              addressController.clear();
                              geocodedCoordinates = null;
                              setDialogState(() {});
                            },
                          )
                        : null,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    filled: true,
                    fillColor: Colors.grey[50],
                  ),
                  maxLines: 2,
                  onChanged: (value) {
                    geocodedCoordinates = null;
                    setDialogState(() {});
                  },
                ),
                const SizedBox(height: 12),
                if (addressController.text.isNotEmpty && geocodedCoordinates == null)
                  Container(
                    width: double.infinity,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      gradient: LinearGradient(
                        colors: [
                          const Color(0xFF1A1A2E).withOpacity(0.1),
                          const Color(0xFF16213E).withOpacity(0.1),
                        ],
                      ),
                    ),
                    child: TextButton.icon(
                      onPressed: isGeocoding
                          ? null
                          : () async {
                              if (addressController.text.trim().isEmpty) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text('Please enter an address'),
                                    backgroundColor: Colors.red,
                                  ),
                                );
                                return;
                              }

                              setDialogState(() {
                                isGeocoding = true;
                              });

                              try {
                                String addressToSearch = addressController.text.trim();
                                List<Location> locations = [];
                                
                                // Try multiple variations for better success rate
                                List<String> searchVariations = [
                                  addressToSearch,
                                  if (!addressToSearch.toLowerCase().contains('dhaka')) '$addressToSearch, Dhaka',
                                  if (!addressToSearch.toLowerCase().contains('bangladesh')) '$addressToSearch, Bangladesh',
                                  if (!addressToSearch.toLowerCase().contains('dhaka') && !addressToSearch.toLowerCase().contains('bangladesh')) '$addressToSearch, Dhaka, Bangladesh',
                                ];

                                // Try each variation until one works
                                for (String variation in searchVariations) {
                                  try {
                                    locations = await locationFromAddress(variation).timeout(
                                      const Duration(seconds: 8),
                                    );
                                    if (locations.isNotEmpty && locations[0].latitude != null && locations[0].longitude != null) {
                                      break; // Found a valid location
                                    }
                                  } catch (e) {
                                    continue; // Try next variation
                                  }
                                }

                                // If still no results, use fallback coordinates (for university project demo)
                                if (locations.isEmpty || locations[0].latitude == null || locations[0].longitude == null) {
                                  // Use approximate coordinates for common locations (for demo purposes)
                                  Map<String, Map<String, double>> fallbackLocations = {
                                    'mirpur': {'latitude': 23.8041, 'longitude': 90.3654},
                                    'dhaka': {'latitude': 23.8103, 'longitude': 90.4125},
                                    'gulshan': {'latitude': 23.7925, 'longitude': 90.4078},
                                    'dhanmondi': {'latitude': 23.7465, 'longitude': 90.3760},
                                    'uttara': {'latitude': 23.8759, 'longitude': 90.3795},
                                  };
                                  
                                  String addressLower = addressToSearch.toLowerCase();
                                  Map<String, double>? fallback = null;
                                  
                                  for (String key in fallbackLocations.keys) {
                                    if (addressLower.contains(key)) {
                                      fallback = fallbackLocations[key];
                                      break;
                                    }
                                  }
                                  
                                  if (fallback != null) {
                                    geocodedCoordinates = fallback;
                                    if (context.mounted) {
                                      ScaffoldMessenger.of(context).showSnackBar(
                                        const SnackBar(
                                          content: Text('Location set (using approximate coordinates for demo)'),
                                          backgroundColor: Colors.blue,
                                          duration: Duration(seconds: 2),
                                        ),
                                      );
                                    }
                                    setDialogState(() {});
                                    return;
                                  }
                                }

                                if (locations.isNotEmpty && locations[0].latitude != null && locations[0].longitude != null) {
                                  final location = locations[0];
                                  geocodedCoordinates = {
                                    'latitude': location.latitude!,
                                    'longitude': location.longitude!,
                                  };
                                  
                                  // Try to get a better formatted address
                                  try {
                                    List<Placemark> placemarks = await placemarkFromCoordinates(
                                      location.latitude!,
                                      location.longitude!,
                                    );
                                    if (placemarks.isNotEmpty) {
                                      final place = placemarks[0];
                                      List<String> addressParts = [];
                                      if (place.street != null && place.street!.isNotEmpty) addressParts.add(place.street!);
                                      if (place.subLocality != null && place.subLocality!.isNotEmpty) addressParts.add(place.subLocality!);
                                      if (place.locality != null && place.locality!.isNotEmpty) addressParts.add(place.locality!);
                                      if (place.subAdministrativeArea != null && place.subAdministrativeArea!.isNotEmpty) addressParts.add(place.subAdministrativeArea!);
                                      if (place.administrativeArea != null && place.administrativeArea!.isNotEmpty) addressParts.add(place.administrativeArea!);
                                      if (place.country != null && place.country!.isNotEmpty) addressParts.add(place.country!);
                                      
                                      final formattedAddress = addressParts.isNotEmpty 
                                          ? addressParts.join(', ')
                                          : addressController.text.trim();
                                      
                                      if (formattedAddress.isNotEmpty && formattedAddress != ', ') {
                                        addressController.text = formattedAddress;
                                      }
                                    }
                                  } catch (e) {
                                    // Keep the original address if reverse geocoding fails
                                    print('Reverse geocoding failed: $e');
                                  }

                                  if (context.mounted) {
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                        content: Text('Location found!'),
                                        backgroundColor: Colors.green,
                                        duration: Duration(seconds: 1),
                                      ),
                                    );
                                  }
                                } else {
                                  if (context.mounted) {
                                    String suggestion = '';
                                    String addressLower = addressToSearch.toLowerCase();
                                    
                                    if (!addressLower.contains('dhaka') && !addressLower.contains('bangladesh')) {
                                      suggestion = '\n\n💡 Tip: Try adding city name, e.g., "$addressToSearch, Dhaka, Bangladesh"';
                                    } else if (!addressLower.contains('bangladesh')) {
                                      suggestion = '\n\n💡 Tip: Try adding country, e.g., "$addressToSearch, Bangladesh"';
                                    } else {
                                      suggestion = '\n\n💡 Tip: Try being more specific with street name or landmark';
                                    }
                                    
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      SnackBar(
                                        content: Text('Address not found.$suggestion'),
                                        backgroundColor: Colors.orange,
                                        duration: const Duration(seconds: 5),
                                        action: SnackBarAction(
                                          label: 'OK',
                                          textColor: Colors.white,
                                          onPressed: () {},
                                        ),
                                      ),
                                    );
                                  }
                                }
                              } catch (e) {
                                print('Geocoding error: $e');
                                if (context.mounted) {
                                  String errorMessage = 'Unable to find this location.';
                                  String suggestion = '';
                                  String addressLower = addressController.text.trim().toLowerCase();
                                  
                                  if (e.toString().contains('timeout') || e.toString().contains('Timeout')) {
                                    errorMessage = 'Request timed out.';
                                    suggestion = '\n\n💡 Please check your internet connection and try again.';
                                  } else if (e.toString().contains('null') || e.toString().contains('No results')) {
                                    if (!addressLower.contains('dhaka') && !addressLower.contains('bangladesh')) {
                                      suggestion = '\n\n💡 Try: "${addressController.text.trim()}, Dhaka, Bangladesh"';
                                    } else if (!addressLower.contains('bangladesh')) {
                                      suggestion = '\n\n💡 Try: "${addressController.text.trim()}, Bangladesh"';
                                    } else {
                                      suggestion = '\n\n💡 Please be more specific (include street name or landmark)';
                                    }
                                  } else {
                                    suggestion = '\n\n💡 Please check your internet connection and try again.';
                                  }
                                  
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    SnackBar(
                                      content: Text('$errorMessage$suggestion'),
                                      backgroundColor: Colors.red,
                                      duration: const Duration(seconds: 5),
                                      action: SnackBarAction(
                                        label: 'OK',
                                        textColor: Colors.white,
                                        onPressed: () {},
                                      ),
                                    ),
                                  );
                                }
                              } finally {
                                setDialogState(() {
                                  isGeocoding = false;
                                });
                              }
                            },
                      icon: isGeocoding
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.search_rounded),
                      label: Text(isGeocoding ? 'Searching...' : 'Search Address'),
                      style: TextButton.styleFrom(
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        foregroundColor: const Color(0xFF1A1A2E),
                      ),
                    ),
                  ),
                if (geocodedCoordinates != null)
                  Container(
                    margin: const EdgeInsets.only(top: 12),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.green.withOpacity(0.1),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(
                        color: Colors.green.withOpacity(0.3),
                        width: 1,
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.check_circle_rounded, color: Colors.green, size: 20),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Location found! Ready to save.',
                            style: TextStyle(
                              color: Colors.green[700],
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () async {
                if (addressController.text.trim().isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Please enter an address'),
                      backgroundColor: Colors.red,
                    ),
                  );
                  return;
                }

                // If coordinates are not already geocoded, try to geocode now
                if (geocodedCoordinates == null) {
                  setDialogState(() {
                    isGeocoding = true;
                  });

                  try {
                    String addressToSearch = addressController.text.trim();
                    List<Location> locations = [];
                    
                    // Try multiple variations
                    List<String> searchVariations = [
                      addressToSearch,
                      if (!addressToSearch.toLowerCase().contains('dhaka')) '$addressToSearch, Dhaka',
                      if (!addressToSearch.toLowerCase().contains('bangladesh')) '$addressToSearch, Bangladesh',
                      if (!addressToSearch.toLowerCase().contains('dhaka') && !addressToSearch.toLowerCase().contains('bangladesh')) '$addressToSearch, Dhaka, Bangladesh',
                    ];

                    for (String variation in searchVariations) {
                      try {
                        locations = await locationFromAddress(variation).timeout(
                          const Duration(seconds: 8),
                        );
                        if (locations.isNotEmpty && locations[0].latitude != null && locations[0].longitude != null) {
                          break;
                        }
                      } catch (e) {
                        continue;
                      }
                    }

                    // Fallback coordinates for demo (university project)
                    if (locations.isEmpty || locations[0].latitude == null || locations[0].longitude == null) {
                      Map<String, Map<String, double>> fallbackLocations = {
                        'mirpur': {'latitude': 23.8041, 'longitude': 90.3654},
                        'dhaka': {'latitude': 23.8103, 'longitude': 90.4125},
                        'gulshan': {'latitude': 23.7925, 'longitude': 90.4078},
                        'dhanmondi': {'latitude': 23.7465, 'longitude': 90.3760},
                        'uttara': {'latitude': 23.8759, 'longitude': 90.3795},
                      };
                      
                      String addressLower = addressToSearch.toLowerCase();
                      Map<String, double>? fallback = null;
                      
                      for (String key in fallbackLocations.keys) {
                        if (addressLower.contains(key)) {
                          fallback = fallbackLocations[key];
                          break;
                        }
                      }
                      
                      if (fallback != null) {
                        geocodedCoordinates = fallback;
                        setDialogState(() {
                          isGeocoding = false;
                        });
                        // Continue to save
                      } else {
                      String suggestion = '';
                      String addressLower = addressToSearch.toLowerCase();
                      
                      if (!addressLower.contains('dhaka') && !addressLower.contains('bangladesh')) {
                        suggestion = '\n\n💡 Try: "$addressToSearch, Dhaka, Bangladesh"';
                      } else if (!addressLower.contains('bangladesh')) {
                        suggestion = '\n\n💡 Try: "$addressToSearch, Bangladesh"';
                      } else {
                        suggestion = '\n\n💡 Please be more specific with street name or landmark';
                      }
                      
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text('Could not find this address.$suggestion'),
                          backgroundColor: Colors.red,
                          duration: const Duration(seconds: 5),
                          action: SnackBarAction(
                            label: 'OK',
                            textColor: Colors.white,
                            onPressed: () {},
                          ),
                        ),
                      );
                        setDialogState(() {
                          isGeocoding = false;
                        });
                        return;
                      }
                    }

                    geocodedCoordinates = {
                      'latitude': locations[0].latitude!,
                      'longitude': locations[0].longitude!,
                    };
                  } catch (e) {
                    print('Geocoding error on save: $e');
                    String errorMessage = 'Unable to find this location.';
                    String suggestion = '';
                    String addressLower = addressController.text.trim().toLowerCase();
                    
                    if (e.toString().contains('timeout') || e.toString().contains('Timeout')) {
                      errorMessage = 'Request timed out.';
                      suggestion = '\n\n💡 Please check your internet connection and try again.';
                    } else if (e.toString().contains('null') || e.toString().contains('No results')) {
                      if (!addressLower.contains('dhaka') && !addressLower.contains('bangladesh')) {
                        suggestion = '\n\n💡 Try: "${addressController.text.trim()}, Dhaka, Bangladesh"';
                      } else if (!addressLower.contains('bangladesh')) {
                        suggestion = '\n\n💡 Try: "${addressController.text.trim()}, Bangladesh"';
                      } else {
                        suggestion = '\n\n💡 Please be more specific (include street name or landmark)';
                      }
                    } else {
                      suggestion = '\n\n💡 Please check your internet connection and try again.';
                    }
                    
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(
                        content: Text('$errorMessage$suggestion'),
                        backgroundColor: Colors.red,
                        duration: const Duration(seconds: 5),
                        action: SnackBarAction(
                          label: 'OK',
                          textColor: Colors.white,
                          onPressed: () {},
                        ),
                      ),
                    );
                    setDialogState(() {
                      isGeocoding = false;
                    });
                    return;
                  } finally {
                    setDialogState(() {
                      isGeocoding = false;
                    });
                  }
                }

                // Save the location
                final location = {
                  'latitude': geocodedCoordinates!['latitude']!,
                  'longitude': geocodedCoordinates!['longitude']!,
                  'address': addressController.text.trim(),
                };

                if (isPickup) {
                  _pickupLocation = location;
                  _pickupController.text = addressController.text.trim();
                } else {
                  _dropLocation = location;
                  _dropController.text = addressController.text.trim();
                }
                
                _updateEstimates();
                Navigator.pop(context);
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF1A1A2E),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
              ),
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _requestRide() async {
    if (_pickupLocation == null || _dropLocation == null) {
      _showError('Please select both pickup and drop locations');
      return;
    }

    if (_selectedVehicleType == null) {
      _showError('Please select a vehicle type');
      return;
    }

    // Check if passenger has sufficient balance
    final walletProvider = Provider.of<WalletProvider>(context, listen: false);
    if (_estimatedFare != null && walletProvider.balance < _estimatedFare!) {
      if (!mounted) return;
      
      // Show modern bottom sheet prompting user to add money
      _showInsufficientBalanceSheet(walletProvider.balance, _estimatedFare!);
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      final rideProvider = Provider.of<RideProvider>(context, listen: false);
      await rideProvider.requestRide(
        pickupLocation: _pickupLocation!,
        dropLocation: _dropLocation!,
        vehicleType: _selectedVehicleType,
      );

      if (!mounted) return;

      // Connect WebSocket if not connected
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      if (!wsService.isConnected) {
        wsService.connect();
      }

      Navigator.pushReplacement(
        context,
        MaterialPageRoute(builder: (_) => const RideStatusScreen()),
      );
    } catch (e) {
      _showError('Failed to request ride: $e');
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _showInsufficientBalanceSheet(double balance, double estimatedFare) {
    final theme = Theme.of(context);
    final needToAdd = estimatedFare - balance;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => Container(
        decoration: BoxDecoration(
          color: theme.colorScheme.surface,
          borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        ),
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom + 24,
          left: 24,
          right: 24,
          top: 24,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Handle bar
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: theme.colorScheme.outline.withOpacity(0.3),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 24),
            // Title
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.errorContainer,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Icon(
                    Icons.warning_rounded,
                    color: theme.colorScheme.onErrorContainer,
                    size: 24,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'Insufficient Balance',
                    style: theme.textTheme.titleLarge?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            // Explanation
            Text(
              'Your wallet balance is insufficient for this ride.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurface.withOpacity(0.7),
              ),
            ),
            const SizedBox(height: 24),
            // Breakdown card
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Current Balance',
                          style: theme.textTheme.bodyMedium,
                        ),
                        Text(
                          '৳${balance.toStringAsFixed(2)}',
                          style: theme.textTheme.bodyLarge?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Estimated Fare',
                          style: theme.textTheme.bodyMedium,
                        ),
                        Text(
                          '৳${estimatedFare.toStringAsFixed(2)}',
                          style: theme.textTheme.bodyLarge?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: theme.colorScheme.error,
                          ),
                        ),
                      ],
                    ),
                    const Divider(height: 24),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Need to Add',
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Text(
                          '৳${needToAdd.toStringAsFixed(2)}',
                          style: theme.textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w700,
                            color: theme.colorScheme.error,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 24),
            // Primary CTA
            PrimaryButton(
              text: 'Add Money',
              icon: Icons.add_rounded,
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const WalletScreen()),
                );
              },
            ),
            const SizedBox(height: 12),
            // Cancel button
            Center(
              child: TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Cancel'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    
    return Scaffold(
      appBar: AppBar(
        title: const Text('Request Ride'),
      ),
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            return SingleChildScrollView(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 8),
                  // Pickup Location
                  LocationTile(
                    label: 'Pickup Location',
                    address: _pickupController.text.isEmpty ? null : _pickupController.text,
                    hintText: 'Select pickup location',
                    icon: Icons.location_on_rounded,
                    iconColor: theme.colorScheme.tertiary,
                    onCurrentLocationTap: _isLoading ? null : () => _getCurrentLocation(true),
                    onEditTap: () => _showLocationDialog(true),
                    isLoading: _isLoading,
                  ),
                  const SizedBox(height: 16),
                  // Drop Location
                  LocationTile(
                    label: 'Drop Location',
                    address: _dropController.text.isEmpty ? null : _dropController.text,
                    hintText: 'Select drop location',
                    icon: Icons.place_rounded,
                    iconColor: theme.colorScheme.error,
                    onCurrentLocationTap: _isLoading ? null : () => _getCurrentLocation(false),
                    onEditTap: () => _showLocationDialog(false),
                    isLoading: _isLoading,
                  ),
                  const SizedBox(height: 32),
                  // Vehicle Type Selection
                  Text(
                    'Select Vehicle Type',
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 16),
                  VehicleSelector(
                    selectedType: _selectedVehicleType,
                    onSelected: (type) {
                      setState(() {
                        _selectedVehicleType = type;
                      });
                      _updateEstimates();
                    },
                    options: const [
                      VehicleOption(
                        type: 'Bike',
                        icon: Icons.two_wheeler_rounded,
                        color: Color(0xFFFF9800), // Soft orange
                      ),
                      VehicleOption(
                        type: 'Car',
                        icon: Icons.directions_car_rounded,
                        color: Color(0xFF0277BD), // Deep blue
                      ),
                      VehicleOption(
                        type: 'Auto',
                        icon: Icons.local_taxi_rounded,
                        color: Color(0xFF4CAF50), // Soft green
                      ),
                    ],
                  ),
                  if (_pickupLocation != null && _dropLocation != null) ...[
                    const SizedBox(height: 32),
                    // Ride Summary Card
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(20.0),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.all(10),
                                  decoration: BoxDecoration(
                                    color: theme.colorScheme.primaryContainer,
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: Icon(
                                    Icons.info_outline_rounded,
                                    color: theme.colorScheme.onPrimaryContainer,
                                    size: 20,
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Text(
                                  'Ride Summary',
                                  style: theme.textTheme.titleMedium?.copyWith(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 24),
                            InfoCard(
                              icon: Icons.route_rounded,
                              label: 'Distance',
                              value: _calculatedDistance != null
                                  ? '${_calculatedDistance!.toStringAsFixed(2)} km'
                                  : 'Calculating...',
                            ),
                            const SizedBox(height: 20),
                            InfoCard(
                              icon: Icons.access_time_rounded,
                              label: 'Estimated Time',
                              value: _estimatedTime != null
                                  ? '$_estimatedTime min'
                                  : 'Calculating...',
                            ),
                            const SizedBox(height: 20),
                            // Highlighted fare card
                            Container(
                              padding: const EdgeInsets.all(16),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.primaryContainer,
                                borderRadius: BorderRadius.circular(14),
                              ),
                              child: InfoCard(
                                icon: Icons.attach_money_rounded,
                                label: 'Estimated Fare',
                                value: _estimatedFare != null
                                    ? '৳${_estimatedFare!.toStringAsFixed(2)}'
                                    : _selectedVehicleType == null
                                        ? 'Select vehicle type'
                                        : 'Calculating...',
                                isHighlight: true,
                                iconColor: theme.colorScheme.onPrimaryContainer,
                                valueColor: theme.colorScheme.onPrimaryContainer,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(height: 32),
                  // Primary CTA Button - Fixed at bottom with safe area
                  Padding(
                    padding: EdgeInsets.only(
                      bottom: MediaQuery.of(context).padding.bottom,
                    ),
                    child: PrimaryButton(
                      text: 'Request Ride',
                      icon: Icons.directions_car_rounded,
                      onPressed: (_isLoading ||
                              _pickupLocation == null ||
                              _dropLocation == null ||
                              _selectedVehicleType == null)
                          ? null
                          : _requestRide,
                      isLoading: _isLoading,
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

}

