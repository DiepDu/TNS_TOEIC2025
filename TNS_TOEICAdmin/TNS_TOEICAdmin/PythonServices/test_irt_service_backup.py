import requests
import json
import random
import uuid
from datetime import datetime

# ============================================================
# TEST SCRIPT FOR IRT ANALYSIS SERVICE
# ============================================================

BASE_URL = "http://localhost:5001"

def print_section(title):
    """Print formatted section header"""
    print("\n" + "="*60)
    print(f"  {title}")
    print("="*60)

def test_health_check():
    """Test 1: Health Check Endpoint"""
    print_section("TEST 1: Health Check")
    
    try:
        response = requests.get(f"{BASE_URL}/health", timeout=5)
        
        print(f"Status Code: {response.status_code}")
        print(f"Response:")
        print(json.dumps(response.json(), indent=2))
        
        if response.status_code == 200:
            print("✅ Health check PASSED")
            return True
        else:
            print("❌ Health check FAILED")
            return False
            
    except Exception as e:
        print(f"❌ Error: {e}")
        return False

def generate_fake_data(num_members=50, num_questions=20, num_responses=150):
    """
    Generate realistic fake IRT data
    
    Giải thích logic tạo dữ liệu:
    - Tạo 50 học viên (members) với ability khác nhau
    - Tạo 20 câu hỏi (questions) với độ khó khác nhau
    - Mỗi member trả lời ngẫu nhiên 5-10 câu hỏi
    - Xác suất trả lời đúng phụ thuộc vào:
        + Ability của member (cao = dễ trả lời đúng)
        + Difficulty của question (cao = khó trả lời đúng)
    """
    print_section("GENERATING FAKE DATA")
    
    # Tạo member IDs
    members = [str(uuid.uuid4()) for _ in range(num_members)]
    
    # Tạo question IDs
    questions = [str(uuid.uuid4()) for _ in range(num_questions)]
    
    # Simulate member abilities (từ -2 đến +2, normal distribution)
    member_abilities = {
        member: random.gauss(0, 1) 
        for member in members
    }
    
    # Simulate question difficulties (từ -2 đến +2)
    question_difficulties = {
        question: random.gauss(0, 1) 
        for question in questions
    }
    
    print(f"📊 Created {num_members} members")
    print(f"📋 Created {num_questions} questions")
    print(f"💭 Generating {num_responses} responses...")
    
    # Generate responses
    responses = []
    response_count = 0
    
    while response_count < num_responses:
        member = random.choice(members)
        question = random.choice(questions)
        
        # Tránh duplicate (same member + same question)
        if any(r['memberKey'] == member and r['questionKey'] == question 
               for r in responses):
            continue
        
        # Calculate probability of correct answer
        # P(correct) dựa trên IRT 1PL model đơn giản
        ability = member_abilities[member]
        difficulty = question_difficulties[question]
        
        # IRT probability function (simplified)
        prob_correct = 1 / (1 + pow(2.718, -(ability - difficulty)))
        
        # Add some randomness
        is_correct = 1 if random.random() < prob_correct else 0
        
        responses.append({
            "memberKey": member,
            "questionKey": question,
            "isCorrect": is_correct
        })
        
        response_count += 1
    
    print(f"✅ Generated {len(responses)} unique responses")
    
    # Statistics
    correct_count = sum(r['isCorrect'] for r in responses)
    print(f"📈 Correct answers: {correct_count}/{len(responses)} ({correct_count/len(responses)*100:.1f}%)")
    
    return {
        "data": responses,
        "metadata": {
            "num_members": num_members,
            "num_questions": num_questions,
            "num_responses": len(responses),
            "generated_at": datetime.utcnow().isoformat()
        }
    }

def test_insufficient_data():
    """Test 2: Insufficient Data (should fail)"""
    print_section("TEST 2: Insufficient Data (Expected to FAIL)")
    
    # Chỉ tạo 50 responses (< 100 minimum)
    fake_data = generate_fake_data(
        num_members=10,
        num_questions=5,
        num_responses=50
    )
    
    try:
        response = requests.post(
            f"{BASE_URL}/analyze",
            json=fake_data,
            timeout=30
        )
        
        print(f"Status Code: {response.status_code}")
        print(f"Response:")
        print(json.dumps(response.json(), indent=2))
        
        if response.status_code == 400:
            print("✅ Correctly rejected insufficient data")
            return True
        else:
            print("❌ Should have rejected insufficient data")
            return False
            
    except Exception as e:
        print(f"❌ Error: {e}")
        return False

def test_valid_analysis():
    """Test 3: Valid IRT Analysis"""
    print_section("TEST 3: Valid IRT Analysis")
    
    # Tạo đủ dữ liệu (>= 100 responses)
    fake_data = generate_fake_data(
        num_members=50,
        num_questions=20,
        num_responses=200
    )
    
    print(f"\n📤 Sending {len(fake_data['data'])} responses to IRT service...")
    print("⏳ This may take 10-30 seconds...")
    
    try:
        response = requests.post(
            f"{BASE_URL}/analyze",
            json=fake_data,
            timeout=60
        )
        
        print(f"\n✅ Status Code: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            
            print(f"\n📊 ANALYSIS RESULTS:")
            print(f"   Status: {result['status']}")
            print(f"   Total Questions Analyzed: {result['metadata']['totalQuestions']}")
            print(f"   Total Members Analyzed: {result['metadata']['totalMembers']}")
            print(f"   Total Responses Used: {result['metadata']['totalResponses']}")
            print(f"   Model Type: {result['metadata']['modelType']}")
            print(f"   Timestamp: {result['metadata']['timestamp']}")
            
            # Show sample question parameters
            print(f"\n📋 SAMPLE QUESTION PARAMETERS (first 3):")
            for i, (q_key, q_params) in enumerate(list(result['questionParams'].items())[:3]):
                print(f"\n   Question {i+1}:")
                print(f"      Key: {q_key[:8]}...")
                print(f"      Difficulty (b): {q_params['difficulty']:.3f}")
                print(f"      Discrimination (a): {q_params['discrimination']:.3f}")
                print(f"      Guessing (c): {q_params['guessing']:.3f}")
                print(f"      Quality: {q_params['quality']}")
                print(f"      Confidence: {q_params['confidenceLevel']}")
                print(f"      Attempt Count: {q_params['attemptCount']}")
            
            # Show sample member abilities
            print(f"\n👥 SAMPLE MEMBER ABILITIES (first 3):")
            for i, (m_key, ability) in enumerate(list(result['memberAbilities'].items())[:3]):
                print(f"   Member {i+1}: {m_key[:8]}... → Ability: {ability:.3f}")
            
            # Analyze quality distribution
            qualities = [q['quality'] for q in result['questionParams'].values()]
            print(f"\n📊 QUALITY DISTRIBUTION:")
            print(f"   Tốt: {qualities.count('Tốt')}")
            print(f"   Cần xem lại: {qualities.count('Cần xem lại')}")
            print(f"   Kém: {qualities.count('Kém')}")
            
            # Analyze confidence distribution
            confidences = [q['confidenceLevel'] for q in result['questionParams'].values()]
            print(f"\n📊 CONFIDENCE DISTRIBUTION:")
            print(f"   High: {confidences.count('High')}")
            print(f"   Medium: {confidences.count('Medium')}")
            print(f"   Low: {confidences.count('Low')}")
            
            print("\n✅ IRT Analysis PASSED")
            return True
        else:
            print(f"❌ Analysis FAILED")
            print(json.dumps(response.json(), indent=2))
            return False
            
    except Exception as e:
        print(f"❌ Error: {e}")
        return False

def run_all_tests():
    """Run all tests"""
    print("\n" + "="*60)
    print("  🧪 STARTING IRT SERVICE TEST SUITE")
    print("="*60)
    
    results = []
    
    # Test 1: Health Check
    results.append(("Health Check", test_health_check()))
    
    # Test 2: Insufficient Data
    results.append(("Insufficient Data Handling", test_insufficient_data()))
    
    # Test 3: Valid Analysis
    results.append(("Valid IRT Analysis", test_valid_analysis()))
    
    # Summary
    print_section("TEST SUMMARY")
    
    passed = sum(1 for _, result in results if result)
    total = len(results)
    
    for test_name, result in results:
        status = "✅ PASS" if result else "❌ FAIL"
        print(f"{status} - {test_name}")
    
    print(f"\n📊 Total: {passed}/{total} tests passed ({passed/total*100:.0f}%)")
    
    if passed == total:
        print("\n🎉 ALL TESTS PASSED!")
    else:
        print(f"\n⚠️  {total - passed} test(s) failed")
    
    return passed == total

if __name__ == "__main__":
    run_all_tests()