from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from datetime import datetime

app = FastAPI(
    title="CEPEM AI Service",
    description="AI mikroservice pro CEPEM platformu",
    version="1.0.0"
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://frontend:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Health check endpoint
@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "service": "AI Service",
        "timestamp": datetime.now().isoformat(),
        "version": "1.0.0"
    }

# Basic info endpoint
@app.get("/api/info")
async def get_info():
    return {
        "service": "CEPEM AI Service",
        "version": "1.0.0",
        "status": "running"
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
