import 'package:flutter/material.dart';

/// Vehicle type selector widget with segmented card buttons
class VehicleSelector extends StatelessWidget {
  final String? selectedType;
  final Function(String) onSelected;
  final List<VehicleOption> options;

  const VehicleSelector({
    super.key,
    required this.selectedType,
    required this.onSelected,
    required this.options,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: options.map((option) {
        final isSelected = selectedType == option.type;
        return Expanded(
          child: Padding(
            padding: EdgeInsets.only(
              right: option == options.last ? 0 : 8,
            ),
            child: _VehicleCard(
              option: option,
              isSelected: isSelected,
              onTap: () => onSelected(option.type),
            ),
          ),
        );
      }).toList(),
    );
  }
}

class VehicleOption {
  final String type;
  final IconData icon;
  final Color color;

  const VehicleOption({
    required this.type,
    required this.icon,
    required this.color,
  });
}

class _VehicleCard extends StatelessWidget {
  final VehicleOption option;
  final bool isSelected;
  final VoidCallback onTap;

  const _VehicleCard({
    required this.option,
    required this.isSelected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 12),
        decoration: BoxDecoration(
          color: isSelected
              ? option.color.withOpacity(0.1)
              : theme.colorScheme.surface,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: isSelected
                ? option.color
                : theme.colorScheme.outline.withOpacity(0.3),
            width: isSelected ? 2 : 1,
          ),
          boxShadow: isSelected
              ? [
                  BoxShadow(
                    color: option.color.withOpacity(0.2),
                    blurRadius: 8,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: option.color.withOpacity(0.15),
                shape: BoxShape.circle,
              ),
              child: Icon(
                option.icon,
                color: option.color,
                size: 24,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              option.type,
              style: theme.textTheme.bodyMedium?.copyWith(
                fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
                color: isSelected
                    ? option.color
                    : theme.colorScheme.onSurface,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

