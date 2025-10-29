#!/bin/bash

# Agent Assistant Backend Deployment Script for AWS EC2

echo "🚀 Starting Agent Assistant Backend Deployment..."

# Update system packages
sudo apt update
sudo apt upgrade -y

# Install Python 3 and pip
sudo apt install python3 python3-pip python3-venv -y

# Install Apache and mod_wsgi
sudo apt install apache2 libapache2-mod-wsgi-py3 -y

# Create project directory
sudo mkdir -p /var/www/agent-assistant-backend
sudo chown -R $USER:$USER /var/www/agent-assistant-backend

# Copy project files
cp -r . /var/www/agent-assistant-backend/

# Create virtual environment
cd /var/www/agent-assistant-backend
python3 -m venv venv
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Create Apache virtual host configuration
sudo tee /etc/apache2/sites-available/agent-assistant-backend.conf > /dev/null <<EOF
<VirtualHost *:80>
    ServerName your-domain.com
    ServerAlias www.your-domain.com
    
    WSGIDaemonProcess agent-assistant-backend python-path=/var/www/agent-assistant-backend python-home=/var/www/agent-assistant-backend/venv
    WSGIProcessGroup agent-assistant-backend
    WSGIScriptAlias / /var/www/agent-assistant-backend/app.wsgi
    
    <Directory /var/www/agent-assistant-backend>
        WSGIProcessGroup agent-assistant-backend
        WSGIApplicationGroup %{GLOBAL}
        Require all granted
    </Directory>
    
    ErrorLog \${APACHE_LOG_DIR}/agent-assistant-backend_error.log
    CustomLog \${APACHE_LOG_DIR}/agent-assistant-backend_access.log combined
</VirtualHost>
EOF

# Enable the site and mod_wsgi
sudo a2ensite agent-assistant-backend
sudo a2enmod wsgi
sudo a2enmod rewrite

# Set proper permissions
sudo chown -R www-data:www-data /var/www/agent-assistant-backend
sudo chmod -R 755 /var/www/agent-assistant-backend

# Restart Apache
sudo systemctl restart apache2
sudo systemctl enable apache2

echo "✅ Deployment completed!"
echo "🌐 Your backend is now available at: http://your-ec2-public-ip/"
echo "📝 Don't forget to:"
echo "   1. Update the ServerName in the Apache config with your actual domain"
echo "   2. Configure your security group to allow HTTP traffic on port 80"
echo "   3. Update the frontend to use your new AWS URL"


