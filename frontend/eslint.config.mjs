import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['**/dist/**', '**/node_modules/**', '**/playwright-report/**', '**/test-results/**'] },
  ...tseslint.configs.recommended,
  {
    files: ['**/*.ts', '**/*.tsx'],
    rules: {
      // L2-014: workspace libraries expose a single public entry point; deep imports are disallowed.
      'no-restricted-imports': ['error', {
        patterns: [
          {
            group: ['@press/*/**'],
            message: 'Deep import into a workspace library (L2-014). Import the package entry point.',
          },
          {
            group: ['**/libs/*/src/**', '../../../libs/**'],
            message: 'Cross-package path import (L2-014). Import via the package name.',
          },
        ],
      }],
    },
  },
);
