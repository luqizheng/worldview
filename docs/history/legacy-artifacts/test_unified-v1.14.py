import sqlite3,pathlib
SQL=pathlib.Path(__file__).with_name('world-view-ai-assistant-unified-schema-v1.14.sql').read_text()
c=sqlite3.connect(':memory:'); c.execute('PRAGMA foreign_keys=ON'); c.executescript(SQL)
assert c.execute('SELECT version, migration_id FROM schema_migrations').fetchone()==(1,'0001_unified_ai_assistant_schema_v1_14')
def expect_fail(label, fn):
    try: fn()
    except sqlite3.IntegrityError as e: print('PASS reject:',label,'->',str(e)); return
    raise AssertionError('expected rejection: '+label)
def task(tid, now):
    c.execute('BEGIN')
    c.execute("INSERT INTO tasks(task_id,title,user_request,intent_type,status,complexity,current_contract_version_number,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?)",(tid,'T','request','map_operation','created','simple',1,now,now))
    c.execute("INSERT INTO task_contract_versions(contract_version_id,task_id,version_number,contract_data,created_at) VALUES(?,?,?,?,?)",('cv-'+tid,tid,1,'{}',now))
    c.commit()
task('t1',1); task('t2',2)
for tid,csid,cid,vid,obj in [('t1','cs1','c1','v1','o1'),('t2','cs2','c2','v2','o2')]:
    c.execute("INSERT INTO candidate_sets VALUES(?,?,?,?,?,?,?,?)",(csid,tid,'set',None,'ai_generated','ready',1,1))
    c.execute("INSERT INTO object_candidates(candidate_id,task_id,candidate_set_id,object_id,current_version_number,created_at,updated_at) VALUES(?,?,?,?,1,1,1)",(cid,tid,csid,obj))
    c.execute("INSERT INTO object_candidate_versions(candidate_version_id,task_id,candidate_id,version_number,payload_ref,created_at) VALUES(?,?,?,?,?,1)",(vid,tid,cid,1,'asset:'+vid))
# Cross-task report item is blocked by two-column report FK and candidate version FK.
c.execute("INSERT INTO validation_reports(validation_report_id,task_id,candidate_version_id,validation_scope,validator_version,status,started_at) VALUES('r1','t1','v1','object_candidate','val1','running',1)")
expect_fail('validation report item cannot mix report Task and candidate Task',lambda:c.execute("INSERT INTO validation_report_items VALUES('ri-bad','r1','t2','v2',1)"))
# Correct manifest and finalization.
c.execute("INSERT INTO validation_report_items VALUES('ri1','r1','t1','v1',1)")
c.execute("UPDATE validation_reports SET status='passed',finished_at=2 WHERE validation_report_id='r1'")
expect_fail('finalized report manifest cannot be edited',lambda:c.execute("DELETE FROM validation_report_items WHERE report_item_id='ri1'"))
# Task-scoped selection links.
c.execute("INSERT INTO selection_snapshots(selection_snapshot_id,task_id,snapshot_number,status,decision_source,created_at) VALUES('ss1','t1',1,'finalized','user',2)")
expect_fail('selection cannot point to another Task candidate',lambda:c.execute("INSERT INTO selection_items VALUES('si-bad','ss1','t1','c2','v2','o2','selected',NULL,2)"))
expect_fail('selection Object ID must match Candidate identity',lambda:c.execute("INSERT INTO selection_items VALUES('si-bad2','ss1','t1','c1','v1','o-wrong','selected',NULL,2)"))
c.execute("INSERT INTO selection_items VALUES('si1','ss1','t1','c1','v1','o1','selected',NULL,2)")
# second candidate for same Object; partial unique index rejects second selected choice.
c.execute("INSERT INTO candidate_sets VALUES('cs1b','t1','set b',NULL,'ai_generated','ready',1,1)")
c.execute("INSERT INTO object_candidates(candidate_id,task_id,candidate_set_id,object_id,current_version_number,created_at,updated_at) VALUES('c1b','t1','cs1b','o1',1,1,1)")
c.execute("INSERT INTO object_candidate_versions(candidate_version_id,task_id,candidate_id,version_number,payload_ref,created_at) VALUES('v1b','t1','c1b',1,'asset:v1b',1)")
expect_fail('snapshot cannot select two versions for same Object',lambda:c.execute("INSERT INTO selection_items VALUES('si2','ss1','t1','c1b','v1b','o1','selected',NULL,2)"))
# Change Set, draft version, item, seal, authorization.
c.execute("INSERT INTO change_sets VALUES('chg1','t1','ss1',1,'draft',3,3,NULL)")
c.execute("INSERT INTO change_set_versions(change_set_version_id,change_set_id,version_number,target_branch_id,base_branch_revision_id,created_at) VALUES('chgv1','chg1',1,'branch-A','rev-1',3)")
c.execute("INSERT INTO change_items(change_item_id,change_set_id,task_id,version_number,item_number,operation_type,object_id,candidate_id,candidate_version_id,payload_ref,created_at) VALUES('item1','chg1','t1',1,1,'create','o1','c1','v1','asset:v1',3)")
# Task-scoped Change Item references must not escape the source Task.
expect_fail('Change Item cannot reference a candidate from another Task',lambda:c.execute("INSERT INTO change_items(change_item_id,change_set_id,task_id,version_number,item_number,operation_type,object_id,candidate_id,candidate_version_id,payload_ref,created_at) VALUES('itembad','chg1','t1',1,99,'create','o2','c2','v2','asset:v2',3)"))
# Can't seal an empty or unhashed version.
expect_fail('cannot seal without SHA-256 hash',lambda:c.execute("UPDATE change_set_versions SET status='sealed' WHERE change_set_id='chg1'"))
h='a'*64
c.execute("UPDATE change_set_versions SET content_hash=?,status='sealed' WHERE change_set_id='chg1'",(h,))
expect_fail('sealed Change Set items are immutable',lambda:c.execute("UPDATE change_items SET payload_ref='tampered' WHERE change_item_id='item1'"))
c.execute("UPDATE change_sets SET status='ready',updated_at=4 WHERE change_set_id='chg1'")
expect_fail('authorization cannot bind another Branch',lambda:c.execute("INSERT INTO change_authorizations VALUES('authbad','chg1',1,?,'branch-B','authorized','human_user','{}',4,NULL,NULL)",(h,)))
c.execute("INSERT INTO change_authorizations VALUES('auth1','chg1',1,?,'branch-A','authorized','human_user','{}',4,NULL,NULL)",(h,))
expect_fail('only one active authorization per version',lambda:c.execute("INSERT INTO change_authorizations VALUES('authdup','chg1',1,?,'branch-A','authorized','human_user','{}',4,NULL,NULL)",(h,)))
# Simulate the successful report + Change Set marker in one transaction (Core Domain writes are not part of this test).
c.execute("INSERT INTO application_reports(application_id,change_set_id,version_number,authorization_id,status,started_at) VALUES('app1','chg1',1,'auth1','prepared',4)")
c.execute("UPDATE change_sets SET status='applying',updated_at=4 WHERE change_set_id='chg1'")
c.execute("UPDATE application_reports SET status='applied',applied_item_count=1,result_branch_revision_id='rev-2',finished_at=5 WHERE application_id='app1'")
c.execute("UPDATE change_sets SET status='applied',applied_at=5,updated_at=5 WHERE change_set_id='chg1'")
expect_fail('already applied Change Set cannot start another attempt',lambda:c.execute("INSERT INTO application_reports(application_id,change_set_id,version_number,authorization_id,status,started_at) VALUES('app2','chg1',1,'auth1','prepared',6)"))
c.execute("UPDATE change_authorizations SET revoked_at=6,revocation_reason='user revoked' WHERE authorization_id='auth1'")
expect_fail('authorization cannot be unrevoked or re-revoked',lambda:c.execute("UPDATE change_authorizations SET revoked_at=7 WHERE authorization_id='auth1'"))
expect_fail('successful Application Report is immutable',lambda:c.execute("UPDATE application_reports SET result_summary='tampered' WHERE application_id='app1'"))
expect_fail('applied Change Set cannot add another version',lambda:c.execute("INSERT INTO change_set_versions(change_set_version_id,change_set_id,version_number,target_branch_id,base_branch_revision_id,created_at) VALUES('chgv2','chg1',2,'branch-A','rev-1',6)"))
# A separate draft Change Set proves that authorization is blocked until its version is sealed.
c.execute("INSERT INTO change_sets VALUES('chg2','t1','ss1',1,'draft',6,6,NULL)")
c.execute("INSERT INTO change_set_versions(change_set_version_id,change_set_id,version_number,target_branch_id,base_branch_revision_id,created_at) VALUES('chgv2','chg2',1,'branch-A','rev-1',6)")
expect_fail('authorization cannot be added to a draft version',lambda:c.execute("INSERT INTO change_authorizations VALUES('authdraft','chg2',1,?,'branch-A','authorized','human_user','{}',6,NULL,NULL)",(h,)))
c.execute("INSERT INTO change_items(change_item_id,change_set_id,task_id,version_number,item_number,operation_type,object_id,candidate_id,candidate_version_id,payload_ref,created_at) VALUES('item2','chg2','t1',1,1,'create','o1','c1','v1','asset:v1',6)")
h2='b'*64
c.execute("UPDATE change_set_versions SET content_hash=?,status='sealed' WHERE change_set_id='chg2'",(h2,))
c.execute("UPDATE change_sets SET status='ready',updated_at=7 WHERE change_set_id='chg2'")
c.execute("INSERT INTO change_authorizations VALUES('auth2','chg2',1,?,'branch-A','authorized','human_user','{}',7,NULL,NULL)",(h2,))
expect_fail('Change Set cannot become Applied without successful report',lambda:c.execute("UPDATE change_sets SET status='applied',applied_at=8 WHERE change_set_id='chg2'"))
c.execute("INSERT INTO application_reports(application_id,change_set_id,version_number,authorization_id,status,started_at) VALUES('app-rollback','chg2',1,'auth2','prepared',8)")
# A forced failure after the successful markers must roll the entire transaction back.
c.commit()
c.execute('BEGIN')
c.execute("UPDATE change_sets SET status='applying' WHERE change_set_id='chg2'")
c.execute("UPDATE application_reports SET status='applied',applied_item_count=1,result_branch_revision_id='rev-rollback',finished_at=9 WHERE application_id='app-rollback'")
c.execute("UPDATE change_sets SET status='applied',applied_at=9 WHERE change_set_id='chg2'")
c.rollback()
assert c.execute("SELECT status FROM change_sets WHERE change_set_id='chg2'").fetchone()[0]=='ready'
assert c.execute("SELECT status FROM application_reports WHERE application_id='app-rollback'").fetchone()[0]=='prepared'
# Task/contract deferred FK and candidate/current-version deferred FK have all committed.
# Candidate may be created before its formal Object ID is reserved; permit one-time binding only.
c.execute("INSERT INTO candidate_sets VALUES('cs-bind','t1','pending bind',NULL,'ai_generated','generating',2,2)")
c.execute("INSERT INTO object_candidates(candidate_id,task_id,candidate_set_id,object_id,current_version_number,created_at,updated_at) VALUES('c-bind','t1','cs-bind',NULL,1,2,2)")
c.execute("INSERT INTO object_candidate_versions(candidate_version_id,task_id,candidate_id,version_number,payload_ref,created_at) VALUES('v-bind','t1','c-bind',1,'asset:bind',2)")
c.execute("UPDATE object_candidates SET object_id='o-bind' WHERE candidate_id='c-bind'")
expect_fail('bound Candidate Object ID cannot be rebound',lambda:c.execute("UPDATE object_candidates SET object_id='o-other' WHERE candidate_id='c-bind'"))
assert c.execute('PRAGMA foreign_key_check').fetchall()==[]
print('PASS foreign_key_check: no violations')
# Immutable source evidence checks.
expect_fail('candidate version payload cannot be overwritten',lambda:c.execute("UPDATE object_candidate_versions SET payload_ref='mutated' WHERE candidate_version_id='v1'"))
expect_fail('selection snapshot cannot be rewritten',lambda:c.execute("UPDATE selection_snapshots SET decision_summary='changed' WHERE selection_snapshot_id='ss1'"))
expect_fail('contract version cannot be overwritten',lambda:c.execute("UPDATE task_contract_versions SET contract_data='{}' WHERE task_id='t1'"))
c.commit()
# Plan pointer and first plan version can be committed in one deferred-FK transaction.
c.execute("BEGIN")
c.execute("INSERT INTO task_plan_versions(plan_version_id,task_id,version_number,plan_summary,status,created_at) VALUES('p1','t1',1,'plan','active',5)")
c.execute("UPDATE tasks SET current_plan_version_number=1 WHERE task_id='t1'")
c.execute("INSERT INTO task_steps(step_id,task_id,plan_version_id,step_number,objective,action_description,acceptance_criteria,status,created_at,updated_at) VALUES('s1','t1','p1',1,'obj','act','[]','pending',5,5)")
c.execute("INSERT INTO task_steps(step_id,task_id,plan_version_id,step_number,objective,action_description,acceptance_criteria,status,finished_at,created_at,updated_at) VALUES('s2','t1','p1',2,'skip','skip','[]','skipped',6,5,6)")
c.commit()
# Finalized report counts must match persisted issues.
c.execute("INSERT INTO validation_reports(validation_report_id,task_id,candidate_version_id,validation_scope,validator_version,status,started_at) VALUES('r2','t1','v1','object_candidate','val1','running',5)")
c.execute("INSERT INTO validation_report_items VALUES('ri2','r2','t1','v1',5)")
c.execute("INSERT INTO validation_issues(validation_issue_id,validation_report_id,issue_code,severity,category,title,description,created_at) VALUES('iss1','r2','E1','error','geometry','bad','bad geom',5)")
expect_fail('report cannot finalize with mismatched issue counts',lambda:c.execute("UPDATE validation_reports SET status='failed',finished_at=6 WHERE validation_report_id='r2'"))
c.execute("UPDATE validation_reports SET error_count=1,status='failed',finished_at=6 WHERE validation_report_id='r2'")
assert c.execute('PRAGMA foreign_key_check').fetchall()==[]
assert c.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
print('PASS integrity_check: ok')
print('PASS test count: 21 negative + 8 lifecycle/transaction + FK/integrity checks')