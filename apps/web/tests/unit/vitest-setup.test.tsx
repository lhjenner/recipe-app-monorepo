import { render, screen } from '@testing-library/react'
import { expect, test } from 'vitest'

test('configures the jsdom environment and jest-dom matchers', () => {
  render(<p>Vitest is ready</p>)

  expect(screen.getByText('Vitest is ready')).toBeInTheDocument()
})
