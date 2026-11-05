import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import RecipeStep from '../../src/components/RecipeStep';

afterEach(() => vi.unstubAllGlobals());

const step = {
  position: 0,
  items: [{
    position: 0,
    kind: 'ingredient' as const,
    text: 'tofu',
    ingredientName: 'tofu',
    quantityText: null,
    unitText: null,
    notesText: null,
  }],
  instructions: 'Cook tofu.',
};

function touch(type: string, time: number, x = 20, primary = true) {
  const event = new MouseEvent(type, { bubbles: true, clientX: x, clientY: 20 });
  Object.defineProperties(event, {
    pointerType: { value: 'touch' },
    isPrimary: { value: primary },
    timeStamp: { value: time },
  });
  fireEvent(screen.getByRole('listitem'), event);
}

function tap(time: number, x = 20) {
  touch('pointerdown', time, x);
  touch('pointerup', time + 30, x);
}

test('recognizes double taps at the threshold but ignores synthesized double clicks', () => {
  vi.stubGlobal('PointerEvent', MouseEvent);
  const toggle = vi.fn();
  render(<RecipeStep {...step} done={false} onToggle={toggle} />);
  tap(100);
  tap(450);
  expect(toggle).toHaveBeenCalledTimes(1);
  const duplicate = new MouseEvent('dblclick', { bubbles: true });
  Object.defineProperty(duplicate, 'timeStamp', { value: 500 });
  fireEvent(screen.getByRole('listitem'), duplicate);
  expect(toggle).toHaveBeenCalledTimes(1);
  const mouse = new MouseEvent('dblclick', { bubbles: true });
  Object.defineProperty(mouse, 'timeStamp', { value: 1200 });
  fireEvent(screen.getByRole('listitem'), mouse);
  expect(toggle).toHaveBeenCalledTimes(2);
});

test('does not mistake slow taps, distant taps, scrolling, cancellation or multiple fingers for double taps', () => {
  vi.stubGlobal('PointerEvent', MouseEvent);
  const toggle = vi.fn();
  render(<RecipeStep {...step} done={false} onToggle={toggle} />);
  tap(100);
  tap(451);
  tap(600, 100);
  touch('pointerdown', 700, 100);
  touch('pointermove', 720, 140);
  touch('pointerup', 730, 100);
  tap(800, 100);
  touch('pointerdown', 900, 100);
  touch('pointercancel', 920, 100);
  tap(1000, 100);
  touch('pointerdown', 1100, 100);
  touch('pointerdown', 1110, 100, false);
  touch('pointerup', 1140, 100);
  expect(toggle).not.toHaveBeenCalled();
});

test('keyboard repeat does not toggle again and instruction-only steps remain non-interactive', () => {
  const toggle = vi.fn();
  const { rerender } = render(<RecipeStep {...step} done={false} onToggle={toggle} />);
  fireEvent.keyDown(screen.getByRole('listitem'), { key: 'Enter' });
  fireEvent.keyDown(screen.getByRole('listitem'), { key: 'Enter', repeat: true });
  expect(toggle).toHaveBeenCalledTimes(1);
  rerender(<RecipeStep {...step} items={[]} done={false} onToggle={toggle} />);
  expect(screen.getByRole('listitem')).not.toHaveAttribute('tabindex');
  fireEvent.doubleClick(screen.getByRole('listitem'));
  fireEvent.keyDown(screen.getByRole('listitem'), { key: 'Enter' });
  expect(toggle).toHaveBeenCalledTimes(1);
});
