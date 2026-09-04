// React requires this flag when a non-Jest runner deliberately wraps updates in act().
(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// React 19 emits this framework-level notice for the legacy renderer before each render.
// Keep the existing renderer tests quiet until their separately tracked DOM-test migration is complete.
const originalConsoleError = console.error;
console.error = (...arguments_: unknown[]) => {
  if (arguments_[0] === 'react-test-renderer is deprecated. See https://react.dev/warnings/react-test-renderer') {
    return;
  }

  originalConsoleError(...arguments_);
};
