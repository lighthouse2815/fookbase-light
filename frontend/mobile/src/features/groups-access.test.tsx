import { render, screen } from '@testing-library/react-native';
import GroupDetail from '../app/groups/[groupId]';
jest.mock('expo-router', () => ({ router: { push: jest.fn() }, useLocalSearchParams: () => ({ groupId: 'g1' }) }));
jest.mock('react-native-safe-area-context', () => ({ SafeAreaView: require('react-native').View }));
jest.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { id: 'u1' } } }) }));
jest.mock('../components/PostCard', () => ({ PostCard: () => <></> }));
jest.mock('../components/UserCard', () => ({ UserCard: () => <></> }));
const mockPost = jest.fn();
jest.mock('../api/groups', () => ({ groupsApi: {} }));
jest.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: jest.fn() }),
  useMutation: () => ({}),
  useQuery: () => ({ data: { name: 'Nhóm riêng', privacy: 'private', viewerRole: null, memberCount: 1 } }),
  useInfiniteQuery: () => ({ data: { pages: [{ items: [ { id: 'secret' } ] }] } }),
}));
test('private group without membership does not render cached posts', () => {
  jest.spyOn(require('../components/PostCard'), 'PostCard').mockImplementation(mockPost);
  render(<GroupDetail />);
  expect(screen.getByText('Nhóm riêng')).toBeTruthy();
  expect(mockPost).not.toHaveBeenCalled();
});
