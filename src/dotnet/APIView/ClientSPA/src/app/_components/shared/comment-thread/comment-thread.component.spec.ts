import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommentThreadComponent } from './comment-thread.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { MessageService } from 'primeng/api';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CommentThreadComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule,
        NoopAnimationsModule
      ],
      providers: [
        MessageService
      ]
    });
    fixture = TestBed.createComponent(CommentThreadComponent);
    component = fixture.componentInstance;
    component.codePanelRowData = new CodePanelRowData();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('avatar rendering', () => {
    it('should show Copilot icon for azure-sdk comments', () => {
      const azureSdkComment = new CommentItemModel();
      azureSdkComment.id = '1';
      azureSdkComment.createdBy = 'azure-sdk';
      azureSdkComment.createdOn = new Date().toISOString();
      azureSdkComment.commentText = 'Copilot suggestion';
      
      component.codePanelRowData!.comments = [azureSdkComment];
      fixture.detectChanges();
      
      const copilotIcon = fixture.nativeElement.querySelector('img[src="/assets/icons/copilot.svg"]');
      expect(copilotIcon).toBeTruthy();
      expect(copilotIcon?.alt).toBe('Azure SDK Copilot');
    });

    it('should show GitHub avatar for regular user comments', () => {
      const regularComment = new CommentItemModel();
      regularComment.id = '1';
      regularComment.createdBy = 'regular-user';
      regularComment.createdOn = new Date().toISOString();
      regularComment.commentText = 'Regular comment';
      
      component.codePanelRowData!.comments = [regularComment];
      fixture.detectChanges();
      
      const githubAvatar = fixture.nativeElement.querySelector('img[src^="https://github.com/regular-user.png"]');
      expect(githubAvatar).toBeTruthy();
      expect(githubAvatar?.alt).toBe('regular-user');
    });
  });

  describe('comment creator name display', () => {
    it('should display "azure-sdk" for azure-sdk comments', () => {
      const azureSdkComment = new CommentItemModel();
      azureSdkComment.id = '1';
      azureSdkComment.createdBy = 'azure-sdk';
      azureSdkComment.createdOn = new Date().toISOString();
      azureSdkComment.commentText = 'Copilot suggestion';
      
      component.codePanelRowData!.comments = [azureSdkComment];
      fixture.detectChanges();
      
      const creatorName = fixture.nativeElement.querySelector('.fw-bold');
      expect(creatorName?.textContent?.trim()).toBe('azure-sdk');
    });

    it('should display actual username for regular user comments', () => {
      const regularComment = new CommentItemModel();
      regularComment.id = '1';
      regularComment.createdBy = 'regular-user';
      regularComment.createdOn = new Date().toISOString();
      regularComment.commentText = 'Regular comment';
      
      component.codePanelRowData!.comments = [regularComment];
      fixture.detectChanges();
      
      const creatorName = fixture.nativeElement.querySelector('.fw-bold');
      expect(creatorName?.textContent?.trim()).toBe('regular-user');
    });
  });

  describe('setCommentResolutionState', () => {
    it ('should select latest user to resolve comment thread', () => {
      const comment1 = {
        id: '1',
        isResolved: true,
        changeHistory: [ {
          changeAction: 'resolved', 
          changedBy: 'test user 1',
        }]
      } as CommentItemModel;
      const comment2 = {
        id: '2',
        isResolved: true,
        changeHistory: [ {
          changeAction: 'resolved', 
          changedBy: 'test user 1',
        },
        {
          changeAction: 'resolved', 
          changedBy: 'test user 2',
        }]
      } as CommentItemModel;
      
      component.codePanelRowData!.comments = [comment1, comment2];
      component.codePanelRowData!.isResolvedCommentThread = true;
      fixture.detectChanges();
      component.setCommentResolutionState();
      expect(component.threadResolvedBy).toBe('test user 2');
    });
  });
});
