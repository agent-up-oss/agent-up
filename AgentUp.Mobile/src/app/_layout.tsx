import { Tabs } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

export default function RootLayout() {
  return (
    <SafeAreaProvider style={styles.safeArea}>
      <StatusBar style="light" />
      <View style={styles.app}>
        <Tabs
          screenOptions={{
            headerShown: false,
            sceneStyle: { backgroundColor: '#000000' },
            tabBarActiveTintColor: '#00d66b',
            tabBarInactiveTintColor: '#aebcb3',
            tabBarStyle: { backgroundColor: '#000000', borderTopColor: '#287038' },
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
      </View>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: '#000000' },
  app: { flex: 1, backgroundColor: '#000000' },
});
