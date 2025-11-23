pipeline {
    agent any

    environment {
        DB_CONN     = credentials('db-conn')
        IRC_NICK    = credentials('irc-nick')
        IRC_SERVER  = credentials('irc-server')
        IRC_PORT    = credentials('irc-port')
        IRC_PASS    = credentials('irc-pass')
        IRC_CHANNEL = credentials('irc-channel')
        OSU_ID      = credentials('osu-id')
        OSU_SECRET  = credentials('osu-secret')
        DB_PASS     = credentials('db-pass')
    }

    stages {
        stage('Build & Publish') {
            steps {
                sh 'git submodule update --init --recursive'
                sh 'docker compose build'
            }
        }

        stage('Deploy') {
            steps {
                sh 'docker compose down'
                sh 'docker compose up -d'
            }
        }
    }

    post {
        failure {
            echo 'Сломалось нахуй.'
        }
        success {
            echo 'Всё чётко.'
        }
    }
}
