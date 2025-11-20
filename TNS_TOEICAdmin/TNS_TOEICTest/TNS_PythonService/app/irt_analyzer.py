import numpy as np
from scipy.optimize import minimize_scalar
from typing import List
from app.models import ResponseData
from config import settings

def irt_probability(theta: float, difficulty: float, discrimination: float = 1.0) -> float:
    """
    Calculate IRT 2PL probability
    P(correct) = 1 / (1 + exp(-discrimination * (theta - difficulty)))
    """
    return 1.0 / (1.0 + np.exp(-discrimination * (theta - difficulty)))

def log_likelihood(theta: float, responses: List[ResponseData]) -> float:
    """
    Calculate negative log-likelihood for optimization
    """
    ll = 0.0
    for r in responses:
        p = irt_probability(theta, r.difficulty)
        # Clip probability to avoid log(0)
        p = np.clip(p, 1e-10, 1 - 1e-10)
        
        if r.isCorrect == 1:
            ll += np.log(p)
        else:
            ll += np.log(1 - p)
    
    return -ll  # Negative for minimization

def calculate_theta(responses: List[ResponseData], initial_theta: float = 0.0) -> float:
    """
    Estimate theta using Maximum Likelihood Estimation
    """
    if len(responses) < settings.IRT_MIN_RESPONSES:
        print(f"[IRT] Warning: Only {len(responses)} responses (min: {settings.IRT_MIN_RESPONSES})")
        return initial_theta
    
    try:
        # Optimize theta in range [-3, 3]
        result = minimize_scalar(
            lambda t: log_likelihood(t, responses),
            bounds=(-3.0, 3.0),
            method='bounded',
            options={'maxiter': settings.IRT_MAX_ITERATIONS}
        )
        
        new_theta = float(result.x)
        print(f"[IRT] Calculated theta: {new_theta:.2f} (from {len(responses)} responses)")
        return new_theta
    
    except Exception as e:
        print(f"[IRT ERROR]: {e}")
        return initial_theta

def interpret_theta(theta: float) -> str:
    """Convert theta to readable level"""
    if theta < -1.5:
        return "Beginner (200-300)"
    elif theta < -0.5:
        return "Elementary (300-400)"
    elif theta < 0.5:
        return "Intermediate (400-600)"
    elif theta < 1.5:
        return "Upper Intermediate (600-750)"
    else:
        return "Advanced (750-990)"