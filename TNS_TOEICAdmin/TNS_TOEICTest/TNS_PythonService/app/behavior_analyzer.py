import numpy as np
from typing import List, Tuple
from app.models import ResponseData, BehaviorScores, BehavioralPatterns

def analyze_behavior(responses: List[ResponseData]) -> Tuple[BehaviorScores, BehavioralPatterns]:
    """
    Comprehensive behavioral analysis
    """
    if not responses:
        return _default_behavior_scores(), _default_behavioral_patterns()
    
    # Calculate metrics
    times = [r.timeSpent for r in responses]
    changes = [r.numberOfAnswerChanges for r in responses]
    correct = [r.isCorrect for r in responses]
    
    avg_time = np.mean(times)
    accuracy = np.mean(correct) * 100
    avg_changes = np.mean(changes)
    
    # Speed Score (0-100, higher = faster)
    # Assume optimal time is 30s per question
    speed_score = max(0, min(100, 100 - (avg_time - 30) * 2))
    
    # Decisiveness Score (0-100, higher = more decisive)
    # Fewer changes = more decisive
    decisiveness_score = max(0, min(100, 100 - avg_changes * 20))
    
    # Stamina (if multiple questions)
    stamina_score = _calculate_stamina(times) if len(times) > 10 else 0.0
    
    # Answer change analysis
    first_correct = 0
    changed_correct = 0
    no_change_correct = 0
    
    for r in responses:
        if r.numberOfAnswerChanges == 0:
            no_change_correct += r.isCorrect
        elif r.numberOfAnswerChanges > 0:
            # Simplified: assume changed answers
            changed_correct += r.isCorrect
        else:
            first_correct += r.isCorrect
    
    first_acc = (first_correct / len(responses)) * 100 if responses else 0
    changed_acc = (changed_correct / len([r for r in responses if r.numberOfAnswerChanges > 0])) * 100 if any(r.numberOfAnswerChanges > 0 for r in responses) else 0
    
    # Pattern detection
    change_pattern = _detect_change_pattern(avg_changes)
    change_impact = _interpret_change_impact(first_acc, changed_acc)
    learner_profile = _determine_learner_profile(speed_score, decisiveness_score, accuracy)
    time_management = _assess_time_management(avg_time, stamina_score)
    
    behavior_scores = BehaviorScores(
        speed=float(speed_score),
        decisiveness=float(decisiveness_score),
        accuracy=float(accuracy),
        avg_time=float(avg_time),
        stamina=float(stamina_score)
    )
    
    behavioral_patterns = BehavioralPatterns(
        change_pattern=change_pattern,
        first_answer_accuracy=float(first_acc),
        changed_answer_accuracy=float(changed_acc),
        answer_change_impact=change_impact,
        learner_profile=learner_profile,
        time_management=time_management
    )
    
    return behavior_scores, behavioral_patterns

def _calculate_stamina(times: List[int]) -> float:
    """
    Detect fatigue by comparing first half vs second half
    """
    if len(times) < 20:
        return 0.0
    
    mid = len(times) // 2
    first_half_avg = np.mean(times[:mid])
    second_half_avg = np.mean(times[mid:])
    
    # If second half is much slower, stamina is lower
    if first_half_avg == 0:
        return 50.0
    
    ratio = second_half_avg / first_half_avg
    stamina = max(0, min(100, 100 - (ratio - 1.0) * 50))
    return float(stamina)

def _detect_change_pattern(avg_changes: float) -> str:
    if avg_changes < 0.3:
        return "Confident - Rarely changes answers"
    elif avg_changes < 0.8:
        return "Moderate - Sometimes revises"
    else:
        return "Indecisive - Frequently changes answers"

def _interpret_change_impact(first_acc: float, changed_acc: float) -> str:
    diff = changed_acc - first_acc
    if diff > 10:
        return "Positive - Changes improve accuracy"
    elif diff < -10:
        return "Negative - Should trust first instinct"
    else:
        return "Neutral - Minimal impact"

def _determine_learner_profile(speed: float, decisiveness: float, accuracy: float) -> str:
    if speed > 70 and decisiveness > 70:
        return "Fast & Confident"
    elif accuracy > 80:
        return "Accurate & Careful"
    elif speed < 40 and decisiveness < 40:
        return "Slow & Uncertain"
    else:
        return "Balanced"

def _assess_time_management(avg_time: float, stamina: float) -> str:
    if avg_time < 20:
        return "Too fast - may be guessing"
    elif avg_time > 60:
        return "Too slow - risk running out of time"
    elif stamina < 40:
        return "Good pace but poor stamina"
    else:
        return "Well-paced"

def _default_behavior_scores() -> BehaviorScores:
    return BehaviorScores(
        speed=50.0,
        decisiveness=50.0,
        accuracy=0.0,
        avg_time=30.0,
        stamina=0.0
    )

def _default_behavioral_patterns() -> BehavioralPatterns:
    return BehavioralPatterns(
        change_pattern="Unknown",
        first_answer_accuracy=0.0,
        changed_answer_accuracy=0.0,
        answer_change_impact="Unknown",
        learner_profile="Unknown",
        time_management="Unknown"
    )