drop table note_collaborators;

drop index ux_notes_share_token;

alter table notes drop column share_token;
