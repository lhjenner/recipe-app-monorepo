import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, test, vi } from 'vitest'
import AppShell from '../../src/AppShell'

beforeEach(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  })
})

test('keeps the user on the current page and permits retry when logout fails', async () => {
  const onLogout = vi.fn()
    .mockRejectedValueOnce(new Error('Network failure'))
    .mockResolvedValueOnce(undefined)

  render(
    <MemoryRouter>
      <AppShell onLogout={onLogout} />
    </MemoryRouter>,
  )

  fireEvent.click(screen.getByRole('button', { name: 'Log out' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to log out. Please try again.')
  expect(onLogout).toHaveBeenCalledTimes(1)

  fireEvent.click(screen.getByRole('button', { name: 'Log out' }))

  await waitFor(() => expect(onLogout).toHaveBeenCalledTimes(2))
  await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
})
