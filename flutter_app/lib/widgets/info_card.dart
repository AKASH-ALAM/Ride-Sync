import 'package:flutter/material.dart';

/// Reusable info card widget with icon, label, and value
class InfoCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;
  final bool isHighlight;
  final Color? iconColor;
  final Color? valueColor;

  const InfoCard({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
    this.isHighlight = false,
    this.iconColor,
    this.valueColor,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final defaultIconColor = isHighlight
        ? theme.colorScheme.primary
        : (iconColor ?? theme.colorScheme.onSurface.withOpacity(0.6));
    final defaultValueColor = isHighlight
        ? theme.colorScheme.primary
        : (valueColor ?? theme.colorScheme.onSurface);

    return Row(
      children: [
        Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: isHighlight
                ? theme.colorScheme.primaryContainer
                : theme.colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Icon(
            icon,
            size: 20,
            color: defaultIconColor,
          ),
        ),
        const SizedBox(width: 16),
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
                value,
                style: theme.textTheme.bodyLarge?.copyWith(
                  fontWeight: isHighlight ? FontWeight.w700 : FontWeight.w600,
                  fontSize: isHighlight ? 18 : 16,
                  color: defaultValueColor,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

