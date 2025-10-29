#!/usr/bin/python3
import sys
import os

# Add the project directory to the Python path
sys.path.insert(0, '/var/www/agent-assistant-backend')

# Import the Flask application
from app import app as application

if __name__ == "__main__":
    application.run()


