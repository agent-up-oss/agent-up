import { useRouter, type Href } from 'expo-router';
import { useMemo } from 'react';
import type { AppShellConfig } from '../controllers/AppShellContext';
import { resolveInnerBack } from '../providers/InnerShellBackProvider';

export function useInnerShell(title: string, fallbackHref: Href): AppShellConfig {
  const router = useRouter();
  return useMemo(() => ({
    title,
    rightAction: null,
    sidebarContent: null,
    backAction: {
      label: 'Back',
      accessibilityLabel: 'Go back',
      onPress: () => {
        if (resolveInnerBack(router.canGoBack()) === 'back') router.back();
        else router.replace(fallbackHref);
      },
    },
  }), [title, fallbackHref, router]);
}
