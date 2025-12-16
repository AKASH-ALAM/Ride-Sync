import 'package:flutter/material.dart';

/// Reusable location tile widget for pickup/drop locations
class LocationTile extends StatelessWidget {
  final String label;
  final String? address;
  final String hintText;
  final IconData icon;
  final Color iconColor;
  final VoidCallback? onCurrentLocationTap;
  final VoidCallback? onEditTap;
  final bool isLoading;

  const LocationTile({
    super.key,
    required this.label,
    this.address,
    required this.hintText,
    required this.icon,
    required this.iconColor,
    this.onCurrentLocationTap,
    this.onEditTap,
    this.isLoading = false,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Row(
          children: [
            // Icon container
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: iconColor.withOpacity(0.1),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(
                icon,
                color: iconColor,
                size: 24,
              ),
            ),
            const SizedBox(width: 16),
            // Address text
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurface.withOpacity(0.6),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    address ?? hintText,
                    style: theme.textTheme.bodyLarge?.copyWith(
                      fontWeight: address != null ? FontWeight.w600 : FontWeight.w400,
                      color: address != null
                          ? theme.colorScheme.onSurface
                          : theme.colorScheme.onSurface.withOpacity(0.4),
                    ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            // Action buttons
            Column(
              children: [
                if (onCurrentLocationTap != null)
                  IconButton(
                    icon: Icon(
                      Icons.my_location_rounded,
                      color: iconColor,
                      size: 20,
                    ),
                    tooltip: 'Get current location',
                    onPressed: isLoading ? null : onCurrentLocationTap,
                    style: IconButton.styleFrom(
                      backgroundColor: iconColor.withOpacity(0.1),
                      padding: const EdgeInsets.all(10),
                      minimumSize: const Size(40, 40),
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    ),
                  ),
                if (onCurrentLocationTap != null && onEditTap != null)
                  const SizedBox(height: 8),
                if (onEditTap != null)
                  IconButton(
                    icon: Icon(
                      Icons.edit_rounded,
                      color: theme.colorScheme.onSurface.withOpacity(0.7),
                      size: 20,
                    ),
                    tooltip: 'Edit location',
                    onPressed: onEditTap,
                    style: IconButton.styleFrom(
                      backgroundColor: theme.colorScheme.surfaceContainerHighest,
                      padding: const EdgeInsets.all(10),
                      minimumSize: const Size(40, 40),
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

