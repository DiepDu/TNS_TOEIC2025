from pydantic_settings import BaseSettings
from typing import List

class Settings(BaseSettings):
    # Server
    HOST: str = "0.0.0.0"
    PORT: int = 5002
    RELOAD: bool = True
    LOG_LEVEL: str = "info"
    
    # CORS
    ALLOWED_ORIGINS: List[str] = ["http://localhost:5000", "http://localhost:5001"]
    
    # IRT
    IRT_DEFAULT_THETA: float = 0.0
    IRT_MIN_RESPONSES: int = 50
    IRT_MAX_ITERATIONS: int = 100
    IRT_CONVERGENCE_THRESHOLD: float = 0.001
    
    # Thresholds
    MIN_THRESHOLD_PART1: int = 18
    MIN_THRESHOLD_PART2: int = 25
    MIN_THRESHOLD_PART3: int = 25
    MIN_THRESHOLD_PART4: int = 25
    MIN_THRESHOLD_PART5: int = 25
    MIN_THRESHOLD_PART6: int = 32
    MIN_THRESHOLD_PART7: int = 25
    
    class Config:
        env_file = ".env"
        case_sensitive = True

settings = Settings()