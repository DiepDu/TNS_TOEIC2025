import requests
import json
import random
import uuid
from datetime import datetime

BASE_URL = "http://localhost:5001"

def print_section(title):
    """Print formatted section header"""
    print("\n" + "="*70)
    print(f"  {title}")
    print("="*70)

def test_health_check():
    """Test 1: Health Check Endpoint"""
    print_section("TEST 1: Health Check")
    
    try:
        response = requests.get(f"{BASE_URL}/health", timeout=5)
        
        print(f"Status Code: {response.status_code}")
        print(f"Response:")
        print(json.dumps(response.json(), indent=2))
        
        if response.status_code == 200:
            data = response.json()
            if "3PL" in data.get("service", "") or "Pyro" in data.get("method", ""):
                print("✅ Full IRT service detected")
                return True
        
        print("❌ Not Full IRT service")
        return False
            
    except Exception as e:
        print(f"❌ Error: {e}")
        return False

def generate_fake_data(num_members=50, num_questions=20, num_responses=200):
    """
    Generate realistic fake IRT data
    
    Args:
        num_members: Number of test takers
        num_questions: Number of questions
        num_responses: Total number of responses
    
    Returns:
        dict with 'data' and 'metadata' keys
    """
    print_section("GENERATING FAKE DATA")
    
    members = [str(uuid.uuid4()) for _ in range(num_members)]
    questions = [str(uuid.uuid4()) for _ in range(num_questions)]
    
    # Simulate member abilities (normal distribution)
    member_abilities = {member: random.gauss(0, 1) for member in members}
    
    # Simulate question difficulties (normal distribution)
    question_difficulties = {question: random.gauss(0, 1) for question in questions}
    
    print(f"📊 Created {num_members} members")
    print(f"📋 Created {num_questions} questions")
    print(f"💭 Generating {num_responses} responses...")
    
    responses = []
    response_count = 0
    
    while response_count < num_responses:
        member = random.choice(members)
        question = random.choice(questions)
        
        # Avoid duplicate (same member + same question)
        if any(r['memberKey'] == member and r['questionKey'] == question 
               for r in responses):
            continue
        
        # Calculate probability of correct answer using 3PL formula
        ability = member_abilities[member]
        difficulty = question_difficulties[question]
        
        # 3PL: P = 0.25 + 0.75 / (1 + exp(-(ability - difficulty)))
        prob_correct = 0.25 + 0.75 / (1 + pow(2.718, -(ability - difficulty)))
        
        # Generate response
        is_correct = 1 if random.random() < prob_correct else 0
        
        responses.append({
            "memberKey": member,
            "questionKey": question,
            "isCorrect": is_correct
        })
        
        response_count += 1
    
    print(f"✅ Generated {len(responses)} unique responses")
    
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
    
    # Only 50 responses (< 100 minimum)
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

def test_full_irt_analysis():
    """Test 3: Full IRT Analysis with EM Algorithm"""
    print_section("TEST 3: Full IRT Analysis")
    
    # Generate sufficient data (>= 100 responses)
    fake_data = generate_fake_data(
        num_members=50,
        num_questions=20,
        num_responses=200
    )
    
    print(f"\n📤 Sending {len(fake_data['data'])} responses to Full IRT service...")
    print("⏳ This may take 30-60 seconds (EM algorithm training)...")
    
    try:
        response = requests.post(
            f"{BASE_URL}/analyze",
            json=fake_data,
            timeout=120  # Longer timeout for full IRT
        )
        
        print(f"\n✅ Status Code: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            
            print(f"\n📊 FULL IRT ANALYSIS RESULTS:")
            print(f"   Status: {result['status']}")
            print(f"   Model Type: {result['metadata']['modelType']}")
            print(f"   Total Questions: {result['metadata']['totalQuestions']}")
            print(f"   Total Members: {result['metadata']['totalMembers']}")
            print(f"   Total Responses: {result['metadata']['totalResponses']}")
            print(f"   Iterations: {result['metadata'].get('iterations', 'N/A')}")
            
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
                print(f"   Member {i+1}: {m_key[:8]}... → θ = {ability:.3f}")
            
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
            
            # Validate ability distribution
            abilities = list(result['memberAbilities'].values())
            print(f"\n📊 ABILITY DISTRIBUTION:")
            print(f"   Mean: {sum(abilities)/len(abilities):.3f}")
            print(f"   Range: [{min(abilities):.3f}, {max(abilities):.3f}]")
            
            print("\n✅ Full IRT Analysis PASSED")
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
    print("\n" + "="*70)
    print("  🧪 FULL IRT SERVICE TEST SUITE")
    print("="*70)
    
    results = []
    
    # Test 1: Health Check
    results.append(("Health Check (Full IRT)", test_health_check()))
    
    # Test 2: Insufficient Data
    results.append(("Insufficient Data Handling", test_insufficient_data()))
    
    # Test 3: Full IRT Analysis
    results.append(("Full IRT Analysis (EM Algorithm)", test_full_irt_analysis()))
    
    # Summary
    print_section("TEST SUMMARY")
    
    passed = sum(1 for _, result in results if result)
    total = len(results)
    
    for test_name, result in results:
        status = "✅ PASS" if result else "❌ FAIL"
        print(f"{status} - {test_name}")
    
    print(f"\n📊 Total: {passed}/{total} tests passed ({passed/total*100:.0f}%)")
    
    if passed == total:
        print("\n🎉 ALL TESTS PASSED! Full IRT is working!")
        print("\n📝 NEXT STEPS:")
        print("   1. Integrate with C# backend")
        print("   2. Test with real database data")
        print("   3. Deploy to production")
    else:
        print(f"\n⚠️  {total - passed} test(s) failed")
        print("   Review the logs above for details")
    
    return passed == total

if __name__ == "__main__":
    run_all_tests()