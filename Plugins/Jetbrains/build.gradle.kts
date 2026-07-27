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
            Integrates the Agent-Up local commit queue with the JetBrains commit workflow.
        """.trimIndent()
        changeNotes = "Initial queue status and next-entry commit message integration."

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
