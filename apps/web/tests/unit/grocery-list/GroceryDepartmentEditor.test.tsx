import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import GroceryDepartmentEditor from '../../../src/features/grocery-list/components/GroceryDepartmentEditor';

test('displays department names in title case and selects by the original ID', () => {
  const onDepartmentChange = vi.fn();
  render(
    <GroceryDepartmentEditor
      storeName="Woolworths"
      departmentId=""
      departments={[
        { id: 'produce', name: 'fruit and vegetables' },
        { id: 'pantry', name: 'baking supplies' },
      ]}
      disabled={false}
      showConfirmation={false}
      isSaving={false}
      canApprove={false}
      onDepartmentChange={onDepartmentChange}
      onApprove={vi.fn()}
      onDiscard={vi.fn()}
      onCancel={vi.fn()}
    />,
  );

  expect(screen.getByRole('option', { name: 'Fruit and Vegetables' })).toHaveValue('produce');
  expect(screen.getByRole('option', { name: 'Baking Supplies' })).toHaveValue('pantry');
  fireEvent.change(screen.getByRole('combobox', { name: 'Woolworths department' }), {
    target: { value: 'produce' },
  });
  expect(onDepartmentChange).toHaveBeenCalledWith('produce');
});
