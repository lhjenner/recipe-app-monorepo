import { useRef, type PointerEvent } from 'react';
import type { RecipeDetail } from '../services/recipesApi';
import { toTitleCase } from '../titleCase';

type Step = RecipeDetail['steps'][number];
type StepItem = Step['items'][number];
type TouchPoint = { time: number; x: number; y: number };

function RecipeStepItem({
  kind, text, ingredientName, quantityText, unitText, notesText,
}: StepItem) {
  const displayName = kind === 'ingredient' ? toTitleCase(ingredientName ?? text) : text;
  const quantityAndUnit = [quantityText, unitText].filter(Boolean).join(' ');

  if (kind === 'heading') {
    return (
      <div className="recipe-step-item recipe-step-heading">
        <h3>{text}</h3>
        {(quantityAndUnit || notesText) && (
          <p className="recipe-step-item-extra">
            {[quantityAndUnit, notesText].filter(Boolean).join(' · ')}
          </p>
        )}
      </div>
    );
  }

  if (kind === 'note') {
    return (
      <div className="recipe-step-item recipe-step-note">
        <p>{text}</p>
        {(quantityAndUnit || notesText) && (
          <p className="recipe-step-item-extra">
            {[quantityAndUnit, notesText].filter(Boolean).join(' · ')}
          </p>
        )}
      </div>
    );
  }

  return (
    <p className="recipe-step-item recipe-step-ingredient">
      {quantityAndUnit && <span className="recipe-step-quantity">{quantityAndUnit} </span>}
      <span>{displayName}</span>
      {notesText && <span className="recipe-step-notes"> ({notesText})</span>}
    </p>
  );
}

function hasRenderableContent(item: StepItem): boolean {
  const name = item.kind === 'ingredient' ? item.ingredientName ?? item.text : item.text;
  return Boolean(
    name.trim() || item.quantityText?.trim() || item.unitText?.trim() || item.notesText?.trim(),
  );
}

export default function RecipeStep({
  position, items, instructions, done, onToggle,
}: Step & { done: boolean; onToggle: () => void }) {
  const visibleItems = items.filter(hasRenderableContent);
  const hasInstructions = Boolean(instructions.trim());
  const isPaired = visibleItems.length > 0 && hasInstructions;
  const touchStart = useRef<TouchPoint | null>(null);
  const previousTap = useRef<TouchPoint | null>(null);
  const lastTouchTime = useRef<number | null>(null);

  function cancelTouch() {
    touchStart.current = null;
    previousTap.current = null;
  }

  function handleTouchEnd(event: PointerEvent<HTMLLIElement>) {
    if (event.pointerType !== 'touch') return;
    lastTouchTime.current = event.timeStamp;
    const start = touchStart.current;
    touchStart.current = null;
    if (!start || event.timeStamp - start.time > 350
      || Math.hypot(event.clientX - start.x, event.clientY - start.y) > 24) {
      previousTap.current = null;
      return;
    }
    const previous = previousTap.current;
    const tap = { time: event.timeStamp, x: event.clientX, y: event.clientY };
    if (previous && tap.time - previous.time <= 350
      && Math.hypot(tap.x - previous.x, tap.y - previous.y) <= 24) {
      previousTap.current = null;
      onToggle();
    } else {
      previousTap.current = tap;
    }
  }

  return (
    <li
      className={`recipe-step${isPaired ? ' recipe-step-paired' : ''}${done ? ' recipe-step-done' : ''}`}
      tabIndex={isPaired ? 0 : undefined}
      aria-label={isPaired ? `Step ${position + 1}, ${done ? 'done' : 'not done'}` : undefined}
      aria-describedby={isPaired ? 'recipe-step-completion-help' : undefined}
      onDoubleClick={isPaired ? (event) => {
        // Touch browsers may also synthesize dblclick after the pointer gesture.
        if (lastTouchTime.current === null || event.timeStamp - lastTouchTime.current > 700) {
          onToggle();
        }
      } : undefined}
      onKeyDown={isPaired ? (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          if (!event.repeat) onToggle();
        }
      } : undefined}
      onPointerDown={isPaired ? (event) => {
        if (event.pointerType !== 'touch') return;
        if (!event.isPrimary) {
          cancelTouch();
          return;
        }
        touchStart.current = { time: event.timeStamp, x: event.clientX, y: event.clientY };
      } : undefined}
      onPointerMove={isPaired ? (event) => {
        const start = touchStart.current;
        if (event.pointerType === 'touch' && start
          && Math.hypot(event.clientX - start.x, event.clientY - start.y) > 24) {
          cancelTouch();
        }
      } : undefined}
      onPointerCancel={isPaired ? cancelTouch : undefined}
      onPointerUp={isPaired ? handleTouchEnd : undefined}
    >
      <h2 className="recipe-step-number">Step {position + 1}</h2>
      {visibleItems.length > 0 && (
        <div className="recipe-step-items">
          {visibleItems.map((item) => <RecipeStepItem key={item.position} {...item} />)}
        </div>
      )}
      {hasInstructions && <p className="recipe-step-instructions">{instructions}</p>}
    </li>
  );
}
