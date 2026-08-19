import { Tabs } from 'expo-router';
import { Text } from 'react-native';

export default function RootLayout() {
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: '#60a5fa',
        tabBarInactiveTintColor: '#9ca3af',
        tabBarStyle: { backgroundColor: '#111827', borderTopColor: '#374151' },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{ title: 'Home', tabBarIcon: ({ color }) => <Text style={{ color }}>⌂</Text> }}
      />
      <Tabs.Screen
        name="settings"
        options={{ title: 'Settings', tabBarIcon: ({ color }) => <Text style={{ color }}>⚙</Text> }}
      />
    </Tabs>
  );
}
