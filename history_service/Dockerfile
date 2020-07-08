FROM python:3.8-slim

# Set the working directory to /app
WORKDIR /app

COPY ./requirements.txt /app/requirements.txt

# Install any needed packages specified in requirements.txt
RUN pip install -i https://mirrors.ustc.edu.cn/pypi/web/simple -r requirements.txt

# Copy the current directory contents into the container at /app
COPY . /app

# Make port 80 available to the world outside this container
EXPOSE 50051

# Define environment variable
ENV ENVIRONMENT production

# Run when the container launches
CMD ["python", "server.py"]
