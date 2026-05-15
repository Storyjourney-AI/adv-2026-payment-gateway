# User Flow (Sample)

## Use Case  
**Task Assignment & Execution System**

Admin creates tasks, assigns them to users, and tracks progress.  
Users can only see and work on tasks assigned to them.

---

## User Levels (Action × Role)

| Action / Capability | Admin | User |
|--------------------|:-----:|:----:|
| View task list | ✅ | ✅* |
| View task details | ✅ | ✅* |
| Create new task | ✅ | ❌ |
| Assign task to user | ✅ | ❌ |
| Edit task title / description | ✅ | ❌ |
| Change task status (Open / In Progress / Done) | ✅ | ✅ |
| Add comments / notes | ✅ | ✅ |
| Upload attachment | ✅ | ✅ |
| Reassign task | ✅ | ❌ |
| Delete task | ✅ | ❌ |
| View all users’ tasks | ✅ | ❌ |

\* User can only view tasks assigned to them.

---

## User Flows

### Flow #1 – Create & Assign Task  
**As Admin**

1. Open Admin Dashboard  
2. Navigate to Task Management  
3. Click **Create New Task**  
4. Fill in task details (title, description, deadline)  
5. Assign task to one or more users  
6. Set initial status to **Open**  
7. Save task  
8. System notifies assigned users  

---

### Flow #2 – User Opens & Works on Task  
**As User**

1. Open **My Tasks**  
2. See list of tasks assigned by Admin  
3. Click a task to open details  
4. Read instructions and deadline  
5. Change status to **In Progress**  
6. Add comments or upload attachments if needed  
7. Mark task as **Done** when completed  
8. System updates task status for Admin  

---

### Flow #3 – Monitor & Follow Up  
**As Admin**

1. Open Task Management  
2. Filter tasks by status or user  
3. Open a specific task  
4. Review user updates and attachments  
5. Add feedback comment or change status if needed  
6. Close task or reassign if required  

---

## Key Rules / Constraints

- Users cannot see tasks not assigned to them  
- Only Admin can create, assign, reassign, or delete tasks  
- Status changes are logged with timestamp and actor  
- Notifications trigger on:
  - Task assignment  
  - Status change  
  - New comment  

---

**End of Sample**
