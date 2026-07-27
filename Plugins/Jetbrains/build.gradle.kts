import org.gradle.jvm.toolchain.JavaToolchainService
import org.gradle.api.tasks.bundling.Zip

plugins {
    java
    id("org.jetbrains.intellij.platform")
}

group = "dev.agentup"
val agentUpVersion = providers.gradleProperty("agentUpVersion")
    .orElse(providers.environmentVariable("AGENTUP_VERSION"))
    .orElse("0.1.0")
version = agentUpVersion.get()

java {
    toolchain {
        languageVersion.set(JavaLanguageVersion.of(21))
    }
}

dependencies {
    testImplementation("org.junit.jupiter:junit-jupiter:5.13.4")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")

    intellijPlatform {
        intellijIdea("2026.1")
        bundledPlugin("Git4Idea")
        pluginVerifier()
    }
}

intellijPlatform {
    pluginConfiguration {
        name = "Agent-Up Commit Queue"
        version.set(agentUpVersion)
        description = """
            <p>
              Agent-Up Commit Queue integrates the local Agent-Up commit queue with the JetBrains Commit tool window.
              It is built for repositories that use the Agent-Up <code>commits</code> workflow to stage one queued
              vertical-slice commit at a time.
            </p>
            <p>
              Requirements:
            </p>
            <ul>
              <li>The Agent-Up CLI must be installed and available as <code>agent-up</code>, or configured under
                <b>Settings | Tools | Agent-Up</b>.</li>
              <li>The opened IDE project must be a local Git repository.</li>
              <li>The CLI must support <code>agent-up commits status --format json</code> and
                <code>agent-up commits next --format json</code>.</li>
            </ul>
            <p>
              Usage:
            </p>
            <ul>
              <li>Open the Commit tool window.</li>
              <li>The Agent-Up logo appears in the commit-message action area.</li>
              <li>A grey icon means the queue is empty; a red icon means the CLI is unavailable or returned an error.</li>
              <li>When queued entries exist, click the Agent-Up logo to run <code>commits next</code>.</li>
              <li>The plugin refreshes Git changes and inserts the queued commit message into the commit-message field.</li>
            </ul>
            <p>
              The plugin does not create commits by itself. It stages the next Agent-Up queue entry so you can review
              the diff and commit manually inside the IDE.
            </p>
        """.trimIndent()
        changeNotes = "Adds Commit tool window integration for the Agent-Up commits queue."

        ideaVersion {
            sinceBuild = "251"
        }
    }

    pluginVerification {
        ides {
            recommended()
        }
    }
}

val toolchains = extensions.getByType<JavaToolchainService>()

tasks {
    test {
        useJUnitPlatform()
        javaLauncher.set(toolchains.launcherFor {
            languageVersion.set(JavaLanguageVersion.of(21))
        })
    }

    patchPluginXml {
        sinceBuild.set("251")
    }

    named("buildSearchableOptions") {
        enabled = false
    }

    named<Zip>("buildPlugin") {
        from("src/main/resources/META-INF/pluginIcon.svg") {
            into("META-INF")
        }
        from("src/main/resources/META-INF/pluginIcon_dark.svg") {
            into("META-INF")
        }
    }
}
