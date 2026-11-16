import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import GroceryQuantityStepper from '../../../src/features/grocery-list/components/GroceryQuantityStepper';

test('preserves fractional purchase precision when stepping existing measured quantities', () => {
  const onChange = vi.fn();
  const { rerender } = render(
    <GroceryQuantityStepper value="0.1" disabled={false} readOnly onChange={onChange} />,
  );
  expect(screen.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Increase quantity' }));
  expect(onChange).toHaveBeenLastCalledWith('1.1');
  rerender(
    <GroceryQuantityStepper value="1.000000001" disabled={false} readOnly onChange={onChange} />,
  );
  fireEvent.click(screen.getByRole('button', { name: 'Decrease quantity' }));
  expect(onChange).toHaveBeenLastCalledWith('1e-9');
});
