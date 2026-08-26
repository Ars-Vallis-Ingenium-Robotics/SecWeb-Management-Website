using Microsoft.EntityFrameworkCore;
using SecWeb.Data;
using SecWeb.Data.Models;

namespace SecWeb.Services
{
    public class ProjectService
    {
        private readonly ApplicationDbContext _db;


        // ---------------------------------------------------------
        // CONSTRUCTOR
        // ---------------------------------------------------------

        public ProjectService(
            ApplicationDbContext db)
        {
            _db = db;
        }


        // =========================================================
        // GET ALL PROJECTS
        // =========================================================

        public async Task<List<Project>> GetProjectsAsync()
        {
            return await _db.Projects
                .AsNoTracking()

                .Include(project =>
                    project.Teams)

                .OrderBy(project =>
                    project.Name)

                .ToListAsync();
        }


        // =========================================================
        // GET ONE PROJECT
        // =========================================================

        public async Task<Project?> GetProjectAsync(
            int projectId)
        {
            return await _db.Projects
                .AsNoTracking()

                .Include(project =>
                    project.Teams)

                .FirstOrDefaultAsync(
                    project =>
                        project.Id == projectId);
        }


        // =========================================================
        // CREATE PROJECT
        // =========================================================

        public async Task<Project> CreateProjectAsync(
            Project project)
        {
            // -----------------------------------------------------
            // CLEAN PROJECT NAME
            // -----------------------------------------------------

            project.Name =
                project.Name.Trim();


            if (string.IsNullOrWhiteSpace(
                project.Name))
            {
                throw new InvalidOperationException(
                    "A project name is required.");
            }


            // -----------------------------------------------------
            // PREVENT DUPLICATE PROJECT NAMES
            // -----------------------------------------------------

            bool duplicateProject =
                await _db.Projects
                    .AnyAsync(
                        existingProject =>
                            existingProject.Name ==
                            project.Name);


            if (duplicateProject)
            {
                throw new InvalidOperationException(
                    "A project with that name already exists.");
            }


            // -----------------------------------------------------
            // NORMAL USER-CREATED PROJECTS ARE NOT PROTECTED
            // -----------------------------------------------------

            project.IsClubWide =
                false;


            project.IsProtected =
                false;


            project.CreatedAt =
                DateTime.UtcNow;


            _db.Projects.Add(
                project);


            await _db.SaveChangesAsync();


            return project;
        }


        // =========================================================
        // DELETE PROJECT
        // =========================================================

        public async Task DeleteProjectAsync(
            int projectId)
        {
            Project? project =
                await _db.Projects

                    .Include(project =>
                        project.Teams)

                    .Include(project =>
                        project.Memberships)

                    .FirstOrDefaultAsync(
                        project =>
                            project.Id ==
                            projectId);


            // -----------------------------------------------------
            // PROJECT MUST EXIST
            // -----------------------------------------------------

            if (project == null)
            {
                throw new InvalidOperationException(
                    "The project could not be found.");
            }


            // -----------------------------------------------------
            // PROTECTED PROJECTS CANNOT BE DELETED
            // -----------------------------------------------------
            //
            // AVI Robotics will be created as:
            //
            // IsClubWide = true
            // IsProtected = true
            //
            // This prevents an Admin from accidentally deleting
            // the permanent AVI Robotics project.
            //

            if (project.IsProtected)
            {
                throw new InvalidOperationException(
                    "This project is protected and cannot be deleted.");
            }


            // -----------------------------------------------------
            // REMOVE PROJECT MEMBERSHIPS
            // -----------------------------------------------------
            //
            // This removes:
            //
            // - Member assignments
            // - Lead assignments
            //
            // It does NOT delete SecWeb user accounts.
            //

            if (project.Memberships.Count > 0)
            {
                _db.ProjectMemberships.RemoveRange(
                    project.Memberships);
            }


            // -----------------------------------------------------
            // REMOVE TEAMS / SUBSYSTEMS
            // -----------------------------------------------------

            if (project.Teams.Count > 0)
            {
                _db.ProjectTeams.RemoveRange(
                    project.Teams);
            }


            // -----------------------------------------------------
            // REMOVE PROJECT
            // -----------------------------------------------------
            //
            // Any WorkLogs connected to this project will later
            // have ProjectId set to null by the database.
            //
            // Their ProjectNameSnapshot will remain so historical
            // time is not lost.
            //

            _db.Projects.Remove(
                project);


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // GET PROJECT MEMBERSHIPS
        // =========================================================

        public async Task<List<ProjectMembership>>
            GetProjectMembershipsAsync(
                int projectId)
        {
            return await _db.ProjectMemberships
                .AsNoTracking()

                .Include(membership =>
                    membership.User)

                .Include(membership =>
                    membership.Team)

                .Where(membership =>
                    membership.ProjectId ==
                    projectId)

                .OrderBy(membership =>
                    membership.Team!.Name)

                .ThenBy(membership =>
                    membership.User!.LastName)

                .ThenBy(membership =>
                    membership.User!.FirstName)

                .ToListAsync();
        }


        // =========================================================
        // MEMBER JOINS TEAM
        // =========================================================

        public async Task JoinTeamAsync(
            int projectId,
            int teamId,
            string userId)
        {
            // -----------------------------------------------------
            // TEAM MUST BELONG TO THE PROJECT
            // -----------------------------------------------------

            ProjectTeam? team =
                await _db.ProjectTeams

                    .FirstOrDefaultAsync(
                        team =>
                            team.Id ==
                                teamId &&

                            team.ProjectId ==
                                projectId);


            if (team == null)
            {
                throw new InvalidOperationException(
                    "The selected team does not exist.");
            }


            // -----------------------------------------------------
            // CHECK FOR EXISTING MEMBERSHIP
            // -----------------------------------------------------

            bool alreadyMember =
                await _db.ProjectMemberships

                    .AnyAsync(
                        membership =>
                            membership.ProjectId ==
                                projectId &&

                            membership.ProjectTeamId ==
                                teamId &&

                            membership.UserId ==
                                userId);


            if (alreadyMember)
            {
                return;
            }


            // -----------------------------------------------------
            // CREATE MEMBER ASSIGNMENT
            // -----------------------------------------------------

            ProjectMembership membership =
                new()
                {
                    ProjectId =
                        projectId,

                    ProjectTeamId =
                        teamId,

                    UserId =
                        userId,

                    Role =
                        ProjectRole.Member,

                    JoinedAt =
                        DateTime.UtcNow
                };


            _db.ProjectMemberships.Add(
                membership);


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // ADMIN ADDS TEAM / SUBSYSTEM
        // =========================================================

        public async Task<ProjectTeam> AddTeamAsync(
            int projectId,
            string teamName)
        {
            // -----------------------------------------------------
            // PROJECT MUST EXIST
            // -----------------------------------------------------

            Project? project =
                await _db.Projects

                    .FirstOrDefaultAsync(
                        project =>
                            project.Id ==
                            projectId);


            if (project == null)
            {
                throw new InvalidOperationException(
                    "The project could not be found.");
            }


            // -----------------------------------------------------
            // CLEAN TEAM NAME
            // -----------------------------------------------------

            string trimmedName =
                teamName.Trim();


            if (string.IsNullOrWhiteSpace(
                trimmedName))
            {
                throw new InvalidOperationException(
                    "A team name is required.");
            }


            // -----------------------------------------------------
            // PREVENT DUPLICATE TEAM NAMES
            // -----------------------------------------------------

            bool duplicateName =
                await _db.ProjectTeams

                    .AnyAsync(
                        team =>
                            team.ProjectId ==
                                projectId &&

                            team.Name ==
                                trimmedName);


            if (duplicateName)
            {
                throw new InvalidOperationException(
                    "A team with that name already exists in this project.");
            }


            // -----------------------------------------------------
            // CREATE TEAM
            // -----------------------------------------------------

            ProjectTeam team =
                new()
                {
                    ProjectId =
                        projectId,

                    Name =
                        trimmedName
                };


            _db.ProjectTeams.Add(
                team);


            await _db.SaveChangesAsync();


            return team;
        }


        // =========================================================
        // ADMIN DELETES TEAM / SUBSYSTEM
        // =========================================================

        public async Task DeleteTeamAsync(
            int projectId,
            int teamId)
        {
            // -----------------------------------------------------
            // FIND TEAM
            // -----------------------------------------------------

            ProjectTeam? team =
                await _db.ProjectTeams

                    .FirstOrDefaultAsync(
                        team =>
                            team.Id ==
                                teamId &&

                            team.ProjectId ==
                                projectId);


            if (team == null)
            {
                throw new InvalidOperationException(
                    "The team could not be found.");
            }


            // -----------------------------------------------------
            // FIND MEMBERSHIPS FOR THIS TEAM
            // -----------------------------------------------------
            //
            // This includes:
            //
            // - Members
            // - Lead
            //

            List<ProjectMembership> memberships =
                await _db.ProjectMemberships

                    .Where(
                        membership =>
                            membership.ProjectId ==
                                projectId &&

                            membership.ProjectTeamId ==
                                teamId)

                    .ToListAsync();


            // -----------------------------------------------------
            // REMOVE TEAM MEMBERSHIPS
            // -----------------------------------------------------

            if (memberships.Count > 0)
            {
                _db.ProjectMemberships.RemoveRange(
                    memberships);
            }


            // -----------------------------------------------------
            // REMOVE TEAM
            // -----------------------------------------------------
            //
            // SecWeb accounts are not deleted.
            //

            _db.ProjectTeams.Remove(
                team);


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // ADMIN ASSIGNS LEAD
        // =========================================================

        public async Task AssignLeadAsync(
            int projectId,
            int teamId,
            string userId)
        {
            // -----------------------------------------------------
            // USER MUST ALREADY BE A MEMBER OF THE TEAM
            // -----------------------------------------------------

            ProjectMembership? selectedMembership =
                await _db.ProjectMemberships

                    .FirstOrDefaultAsync(
                        membership =>
                            membership.ProjectId ==
                                projectId &&

                            membership.ProjectTeamId ==
                                teamId &&

                            membership.UserId ==
                                userId);


            if (selectedMembership == null)
            {
                throw new InvalidOperationException(
                    "The selected user must be a member of this team before becoming its Lead.");
            }


            // -----------------------------------------------------
            // FIND CURRENT LEAD
            // -----------------------------------------------------
            //
            // Each team currently has a maximum of one Lead.
            //

            List<ProjectMembership> currentLeads =
                await _db.ProjectMemberships

                    .Where(
                        membership =>
                            membership.ProjectId ==
                                projectId &&

                            membership.ProjectTeamId ==
                                teamId &&

                            membership.Role ==
                                ProjectRole.Lead)

                    .ToListAsync();


            // -----------------------------------------------------
            // DEMOTE EXISTING LEAD
            // -----------------------------------------------------

            foreach (
                ProjectMembership currentLead
                in currentLeads)
            {
                currentLead.Role =
                    ProjectRole.Member;
            }


            // -----------------------------------------------------
            // PROMOTE SELECTED MEMBER
            // -----------------------------------------------------

            selectedMembership.Role =
                ProjectRole.Lead;


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // ADMIN REMOVES LEAD ROLE
        // =========================================================

        public async Task RemoveLeadAsync(
            int projectId,
            int teamId)
        {
            // -----------------------------------------------------
            // FIND LEAD
            // -----------------------------------------------------

            List<ProjectMembership> leads =
                await _db.ProjectMemberships

                    .Where(
                        membership =>
                            membership.ProjectId ==
                                projectId &&

                            membership.ProjectTeamId ==
                                teamId &&

                            membership.Role ==
                                ProjectRole.Lead)

                    .ToListAsync();


            // -----------------------------------------------------
            // RETURN LEAD TO MEMBER
            // -----------------------------------------------------
            //
            // They remain:
            //
            // - A SecWeb user
            // - A project member
            // - A team member
            //

            foreach (
                ProjectMembership lead
                in leads)
            {
                lead.Role =
                    ProjectRole.Member;
            }


            await _db.SaveChangesAsync();
        }
    }
}