from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from app.models import AnalysisRequest, AnalysisResponse, PartInsight, SkillLevelAnalysis, WeaknessAnalysis  # ✅ THÊM WeaknessAnalysis
from app.irt_analyzer import calculate_theta, interpret_theta
from app.behavior_analyzer import analyze_behavior
from app.weakness_analyzer import analyze_weaknesses
from config import settings
import logging

# Configure logging
logging.basicConfig(
    level=getattr(logging, settings.LOG_LEVEL.upper()),
    format='[%(asctime)s] %(levelname)s: %(message)s'
)
logger = logging.getLogger(__name__)

# Initialize FastAPI
app = FastAPI(
    title="TOEIC Analysis Service",
    description="IRT-based learning analysis for TOEIC test results",
    version="1.0.0"
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
async def root():
    return {
        "service": "TOEIC Analysis API",
        "version": "1.0.0",
        "status": "running"
    }

@app.get("/health")
async def health():
    return {"status": "healthy"}

@app.post("/analyze_result", response_model=AnalysisResponse)
async def analyze_result(request: AnalysisRequest):
    """
    Main analysis endpoint
    """
    try:
        logger.info(f"Received analysis request: {len(request.responses)} responses, calculate_ability={request.calculate_ability}")
        
        if not request.responses:
            raise HTTPException(status_code=400, detail="No responses provided")
        
        # 1. Calculate Theta
        new_theta = request.current_theta
        if request.calculate_ability:
            new_theta = calculate_theta(request.responses, request.current_theta)
            logger.info(f"Calculated new theta: {new_theta:.2f}")
        
        # 2. Behavior Analysis
        behavior_scores, behavioral_patterns = analyze_behavior(request.responses)
        logger.info(f"Behavior: Speed={behavior_scores.speed:.1f}, Accuracy={behavior_scores.accuracy:.1f}%")
        
        # 3. Weakness Analysis
        weakness_analysis = analyze_weaknesses(request.responses)
        logger.info(f"Weaknesses: {len(weakness_analysis.top_grammar)} grammar, {len(weakness_analysis.top_vocab)} vocab")
        
        # 4. Skill Level
        skill_level = SkillLevelAnalysis(
            comfort_zone=interpret_theta(new_theta),
            challenge_level="Appropriate" if abs(new_theta - request.current_theta) < 0.5 else "Challenging"
        )
        
        # 5. Part-specific insights (simplified for now)
        parts = set(r.part for r in request.responses)
        part_insights = {}
        
        for part_num in parts:
            part_responses = [r for r in request.responses if r.part == part_num]
            part_acc = (sum(r.isCorrect for r in part_responses) / len(part_responses)) * 100
            part_avg_time = sum(r.timeSpent for r in part_responses) / len(part_responses)
            
            strength = "Strong" if part_acc > 80 else "Moderate" if part_acc > 60 else "Weak"
            
            part_insights[f"part{part_num}"] = PartInsight(
                strength=strength,
                accuracy=float(part_acc),
                avg_time=float(part_avg_time),
                weak_areas=[],
                advice=f"Focus on improving accuracy for Part {part_num}"
            )
        
        # 6. Actionable Recommendations
        recommendations = _generate_recommendations(
            behavior_scores.accuracy,
            behavior_scores.speed,
            behavior_scores.decisiveness,
            weakness_analysis
        )
        
        response = AnalysisResponse(
            new_theta=float(new_theta),
            behavior_scores=behavior_scores,
            weakness_analysis=weakness_analysis,
            behavioral_patterns=behavioral_patterns,
            skill_level_analysis=skill_level,
            part_specific_insights=part_insights,
            actionable_recommendations=recommendations
        )
        
        logger.info("Analysis completed successfully")
        return response
    
    except Exception as e:
        logger.error(f"Analysis error: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))

def _generate_recommendations(accuracy: float, speed: float, decisiveness: float, weaknesses: WeaknessAnalysis) -> list:
    """Generate actionable recommendations"""
    recs = []
    
    if accuracy < 60:
        recs.append("Focus on accuracy over speed - take time to understand questions")
    
    if speed < 40:
        recs.append("Practice with time limits to improve speed")
    
    if decisiveness < 50:
        recs.append("Trust your first instinct - over-thinking hurts accuracy")
    
    if weaknesses.top_grammar:
        recs.append(f"Study grammar: {weaknesses.top_grammar[0].split('(')[0].strip()}")
    
    if weaknesses.top_vocab:
        recs.append(f"Learn vocabulary: {weaknesses.top_vocab[0].split('(')[0].strip()}")
    
    if not recs:
        recs.append("Great job! Keep practicing to maintain your level")
    
    return recs

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "app.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.RELOAD,
        log_level=settings.LOG_LEVEL
    )