/**
 * Props for the pasted-code field.
 *
 * Android IME suggestions and autofill rewrite a code as the person types it. The CLI then
 * exchanges that mangled value and the sign-in fails with a generic exit code. This field is a
 * one-time secret, not a password or a sentence, so suggestions stay off on every platform.
 */
export function codeFieldProps() {
  return {
    autoCapitalize: 'none' as const,
    autoCorrect: false,
    autoComplete: 'off' as const,
    spellCheck: false,
    textContentType: 'none' as const,
    importantForAutofill: 'no' as const,
    showSoftInputOnFocus: false,
    keyboardType: 'visible-password' as const,
  };
}
