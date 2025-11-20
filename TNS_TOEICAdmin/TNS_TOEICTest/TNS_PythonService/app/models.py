from pydantic import BaseModel, Field
from typing import List, Optional, Dict

class ResponseData(BaseModel):
    """Single response from user"""
    isCorrect: int = Field(..., ge=0, le=1)
    timeSpent: int = Field(..., ge=0)
    numberOfAnswerChanges: int = Field(..., ge=0)
    difficulty: float = Field(..., ge=-3, le=3)
    grammarName: Optional[str] = None
    vocabName: Optional[str] = None
    errorType: Optional[str] = None
    categoryName: Optional[str] = None
    part: int = Field(..., ge=1, le=7)

class AnalysisRequest(BaseModel):
    """Request payload from C#"""
    current_theta: float = Field(default=0.0, ge=-3, le=3)
    responses: List[ResponseData]
    calculate_ability: bool = False

class BehaviorScores(BaseModel):
    speed: float
    decisiveness: float
    accuracy: float
    avg_time: float
    stamina: float = 0.0

class WeaknessAnalysis(BaseModel):
    top_grammar: List[str] = []
    top_vocab: List[str] = []
    top_error_types: List[str] = []
    top_categories: List[str] = []
    summary: str = ""

class BehavioralPatterns(BaseModel):
    change_pattern: str
    first_answer_accuracy: float
    changed_answer_accuracy: float
    answer_change_impact: str
    learner_profile: str
    time_management: str

class SkillLevelAnalysis(BaseModel):
    comfort_zone: str
    challenge_level: str

class PartInsight(BaseModel):
    strength: str
    accuracy: float
    avg_time: float
    weak_areas: List[str] = []
    advice: str = ""

class AnalysisResponse(BaseModel):
    """Response payload to C#"""
    new_theta: float
    behavior_scores: BehaviorScores
    weakness_analysis: WeaknessAnalysis
    behavioral_patterns: BehavioralPatterns
    skill_level_analysis: SkillLevelAnalysis
    part_specific_insights: Dict[str, PartInsight]
    actionable_recommendations: List[str]